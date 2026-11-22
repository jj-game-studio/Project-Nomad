<#
.SYNOPSIS
Unity Test Runner를 배치 모드로 실행하고 결과를 JSON으로 요약합니다.

.EXAMPLE
./scripts/Test-Unity.ps1                       # EditMode 후 PlayMode 전체
./scripts/Test-Unity.ps1 -Platform EditMode    # EditMode만
./scripts/Test-Unity.ps1 -Platform PlayMode -AssemblyNames Nomad.Monsters.PlayModeTests -TestFilter MonsterTrackingTests
#>
[CmdletBinding()]
param(
    [ValidateSet('All', 'EditMode', 'PlayMode')] [string] $Platform = 'All',
    [string[]] $AssemblyNames,
    [string] $TestFilter,
    [string] $ProjectPath = (Join-Path $PSScriptRoot '../UnityProject'),
    [string] $UnityPath,
    [ValidateRange(1, 3600)] [int] $TimeoutSeconds = 900,
    # 카메라 테스트는 RenderTexture가 필요해 기본은 그래픽 장치를 사용합니다. GPU 없는 환경에서만 켭니다.
    [switch] $NoGraphics
)

$ErrorActionPreference = 'Stop'

function Assert-SafeArgument([string] $Value, [string] $Name) {
    if ($Value.Contains('"') -or $Value.Contains("`n") -or $Value.Contains("`r")) {
        throw "${Name}에 따옴표나 줄바꿈을 사용할 수 없습니다."
    }
}

function Invoke-UnityTests([string] $TestPlatform, [string] $RunDirectory) {
    $resultPath = Join-Path $RunDirectory "$TestPlatform.xml"
    $logPath = Join-Path $RunDirectory "$TestPlatform.log"
    $arguments = @(
        '-batchmode', '-runTests', '-testPlatform', $TestPlatform,
        '-projectPath', ('"' + $ProjectPath + '"'),
        '-testResults', ('"' + $resultPath + '"'),
        '-logFile', ('"' + $logPath + '"')
    )
    if ($NoGraphics) { $arguments += '-nographics' }
    if ($AssemblyNames) { $arguments += @('-assemblyNames', ('"' + ($AssemblyNames -join ';') + '"')) }
    if ($TestFilter) { $arguments += @('-testFilter', ('"' + $TestFilter + '"')) }

    $failure = $null
    $result = $null
    $timedOut = $false
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()

    try {
        $process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
        while (-not $process.WaitForExit(1000)) {
            if ($stopwatch.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
                $timedOut = $true
                # 이 실행기가 시작한 Unity와 자식 프로세스만 종료합니다. Windows PowerShell 5.1과 7 모두 지원합니다.
                & taskkill.exe /PID $process.Id /T /F | Out-Null
                $process.WaitForExit(5000) | Out-Null
                throw "제한 시간 ${TimeoutSeconds}초를 초과했습니다."
            }
        }

        if (-not (Test-Path -LiteralPath $resultPath)) {
            throw "결과 XML이 없습니다. 컴파일 오류 여부를 로그에서 확인하세요. Unity 종료 코드: $($process.ExitCode)"
        }
        [xml] $result = [IO.File]::ReadAllText($resultPath)
        $run = $result.'test-run'
        if ($null -eq $run -or [int] $run.total -eq 0) {
            throw '실행한 테스트가 0개입니다. 어셈블리와 필터를 확인하세요.'
        }
        if ([int] $run.passed -eq 0 -and [int] $run.failed -eq 0) {
            throw '모든 테스트가 건너뛰어졌습니다.'
        }
        if ($process.ExitCode -ne 0 -or $run.result -ne 'Passed') {
            $failure = "테스트 실패 또는 실행 오류. Unity 종료 코드: $($process.ExitCode)"
        }
    }
    catch {
        $failure = $_.Exception.Message
    }
    $stopwatch.Stop()

    $failedTests = @()
    if ($null -ne $result) {
        $failedTests = @($result.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {
            $node = $_.SelectSingleNode('failure/message')
            $message = if ($node) { $node.InnerText.Trim() } else { '' }
            if ($message.Length -gt 1200) { $message = $message.Substring(0, 1200) }
            [ordered] @{ name = [string] $_.fullname; message = $message }
        })
    }

    # 원본 로그는 보존하고 요약에는 예외·컴파일 오류의 첫 줄만 최대 5개 표시합니다.
    $logDiagnostics = @()
    if (Test-Path -LiteralPath $logPath) {
        $logDiagnostics = @(Select-String -LiteralPath $logPath -Pattern '^[A-Za-z.]+Exception:|error CS\d+' |
            Select-Object -First 5 | ForEach-Object { $_.Line })
    }

    $run = if ($result) { $result.'test-run' } else { $null }
    return [ordered] @{
        platform = $TestPlatform
        success = ($null -eq $failure)
        total = $(if ($run) { [int] $run.total } else { 0 })
        passed = $(if ($run) { [int] $run.passed } else { 0 })
        failed = $(if ($run) { [int] $run.failed } else { 0 })
        skipped = $(if ($run) { [int] $run.skipped } else { 0 })
        seconds = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1)
        timedOut = $timedOut
        error = $failure
        failedTests = $failedTests
        logDiagnostics = $logDiagnostics
        results = $resultPath
    }
}

$summaries = @()
try {
    if ($TestFilter) { Assert-SafeArgument $TestFilter '테스트 필터' }
    foreach ($name in $AssemblyNames) { Assert-SafeArgument $name '어셈블리 이름' }

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
        throw '이 프로젝트가 Unity 에디터에서 열려 있습니다. 에디터를 닫거나 별도 작업 사본에서 실행해 주세요.'
    }

    # 실행마다 결과 폴더를 분리하여 이전 XML을 이번 결과로 오인하지 않습니다.
    $runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
    $runDirectory = Join-Path $ProjectPath "Artifacts/tests/unity/$runId"
    New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

    $platforms = if ($Platform -eq 'All') { @('EditMode', 'PlayMode') } else { @($Platform) }
    foreach ($testPlatform in $platforms) {
        $summaries += Invoke-UnityTests $testPlatform $runDirectory
    }
}
catch {
    $summaries += [ordered] @{ platform = $Platform; success = $false; error = $_.Exception.Message }
}

$success = @($summaries | Where-Object { -not $_.success }).Count -eq 0
[ordered] @{ success = $success; runs = $summaries } | ConvertTo-Json -Depth 6 | Write-Output
if ($success) { exit 0 }
exit 1
