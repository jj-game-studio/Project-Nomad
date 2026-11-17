[CmdletBinding()]
param(
    [string] $ProjectPath = (Join-Path $PSScriptRoot '../UnityProject'),
    [string] $UnityPath,
    [string] $TestFilter,
    [ValidateRange(1, 1800)] [int] $TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$process = $null
$timedOut = $false
$failure = $null
$result = $null
$runDirectory = $null
$stopwatch = [Diagnostics.Stopwatch]::StartNew()

try {
    $ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
    $versionFile = Join-Path $ProjectPath 'ProjectSettings/ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $versionFile)) {
        throw 'Unity 프로젝트의 ProjectVersion.txt를 찾을 수 없습니다.'
    }

    if (-not $UnityPath) {
        $version = [regex]::Match([IO.File]::ReadAllText($versionFile), 'm_EditorVersion: (\S+)').Groups[1].Value
        $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
    }
    if (-not (Test-Path -LiteralPath $UnityPath)) {
        throw 'Unity 실행 파일을 찾을 수 없습니다. -UnityPath로 지정해 주세요.'
    }
    if (Test-Path -LiteralPath (Join-Path $ProjectPath 'Temp/UnityLockfile')) {
        throw '이 프로젝트가 Unity에서 사용 중입니다. 별도 작업 사본에서 실행해 주세요.'
    }

    # 실행마다 결과 폴더를 분리하여 이전 XML을 이번 성공으로 오인하지 않습니다.
    $runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $runDirectory = Join-Path $ProjectPath "Artifacts/tests/monster-tracking/$runId"
    New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
    $resultPath = Join-Path $runDirectory 'results.xml'
    $logPath = Join-Path $runDirectory 'unity.log'
    $unityArguments = @(
        '-batchmode', '-nographics', '-runTests', '-testPlatform', 'PlayMode',
        '-assemblyNames', 'Nomad.Monsters.PlayModeTests',
        '-projectPath', ('"' + $ProjectPath + '"'),
        '-testResults', ('"' + $resultPath + '"'),
        '-logFile', ('"' + $logPath + '"')
    )
    if ($TestFilter) {
        if ($TestFilter.Contains('"') -or $TestFilter.Contains("`n") -or $TestFilter.Contains("`r")) {
            throw '테스트 필터에 따옴표나 줄바꿈을 사용할 수 없습니다.'
        }
        $unityArguments += @('-testFilter', ('"' + $TestFilter + '"'))
    }

    $process = Start-Process -FilePath $UnityPath -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
    while (-not $process.WaitForExit(1000)) {
        if ($stopwatch.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
            $timedOut = $true
            # 이 실행기가 시작한 프로세스와 자식만 종료합니다.
            $process.Kill($true)
            $process.WaitForExit(5000) | Out-Null
            throw "검증 제한 시간 ${TimeoutSeconds}초를 초과했습니다."
        }
    }

    if (-not (Test-Path -LiteralPath $resultPath)) {
        throw "Test Runner 결과 XML이 없습니다. Unity 종료 코드: $($process.ExitCode)"
    }
    [xml] $result = [IO.File]::ReadAllText($resultPath)
    if ($null -eq $result.'test-run' -or [int] $result.'test-run'.total -eq 0) {
        throw '실행한 테스트가 0개입니다. 어셈블리와 필터를 확인해 주세요.'
    }
    if ([int] $result.'test-run'.passed -eq 0 -and [int] $result.'test-run'.failed -eq 0) {
        throw '실제 실행된 테스트가 없습니다. 모든 테스트가 건너뛰어졌는지 확인해 주세요.'
    }
    if ($process.ExitCode -ne 0 -or $result.'test-run'.result -ne 'Passed') {
        $failure = "테스트 실패 또는 실행 오류. Unity 종료 코드: $($process.ExitCode)"
    }
}
catch {
    $failure = $_.Exception.Message
}
finally {
    $stopwatch.Stop()
}

$failedTests = @()
if ($null -ne $result) {
    $failedTests = @($result.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {
        $message = [string] $_.failure.message
        if ($message.Length -gt 1200) { $message = $message.Substring(0, 1200) }
        [ordered] @{ name = [string] $_.fullname; message = $message }
    })
}

$logDiagnostics = @()
if ($runDirectory -and (Test-Path -LiteralPath (Join-Path $runDirectory 'unity.log'))) {
    # 원본 로그는 보존하고 요약에는 예외/컴파일 오류의 첫 줄만 최대 5개 표시합니다.
    $logDiagnostics = @(Select-String -LiteralPath (Join-Path $runDirectory 'unity.log') `
        -Pattern '^[A-Za-z.]+Exception:|error CS\d+' | Select-Object -First 5 | ForEach-Object { $_.Line })
}

$summary = [ordered] @{
    success = ($null -eq $failure)
    total = $(if ($result) { [int] $result.'test-run'.total } else { 0 })
    passed = $(if ($result) { [int] $result.'test-run'.passed } else { 0 })
    failed = $(if ($result) { [int] $result.'test-run'.failed } else { 0 })
    skipped = $(if ($result) { [int] $result.'test-run'.skipped } else { 0 })
    seconds = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1)
    timedOut = $timedOut
    error = $failure
    failedTests = $failedTests
    logDiagnostics = $logDiagnostics
    artifacts = $runDirectory
}
$summary | ConvertTo-Json -Depth 5 | Write-Output
if ($failure) { exit 1 }
exit 0
