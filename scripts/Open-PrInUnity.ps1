#Requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 2147483647)]
    [int]$PrNumber,

    [switch]$PrepareOnly,
    [switch]$CheckOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = 'jj-game-studio/Project-Nomad'
$repositoryUrl = "https://github.com/$repository.git"
if (-not $env:LOCALAPPDATA) { throw 'LOCALAPPDATA 환경 변수가 필요합니다.' }

$reviewPath = Join-Path $env:LOCALAPPDATA 'ProjectNomad/PRReview'
$projectPath = Join-Path $reviewPath 'UnityProject'

function Invoke-Checked {
    param([string]$Program, [string[]]$Arguments)

    $output = & $Program @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Program 실행 실패 (종료 코드 $LASTEXITCODE). 기존 파일은 자동 삭제하지 않습니다."
    }

    return $output
}

function Invoke-ReviewGit {
    param([string[]]$Arguments)

    Invoke-Checked git (@('-c', 'core.hooksPath=NUL', '-C', $reviewPath) + $Arguments)
}

function Assert-ProjectClosed {
    # Unity 시작 직후에도 생기는 잠금 파일을 기준으로 보수적으로 중단합니다.
    if (Test-Path -LiteralPath (Join-Path $projectPath 'Temp/UnityLockfile')) {
        throw "Unity 잠금 파일이 있습니다. 검토 프로젝트를 종료하고 확인해 주세요: $projectPath"
    }

    # 종료되지 않은 Editor가 잠금 파일을 지운 경우도 확인합니다.
    $editors = Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction Stop
    foreach ($editor in $editors) {
        if ($editor.CommandLine -and
            $editor.CommandLine.Replace('\', '/').IndexOf(
                $projectPath.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "검토 프로젝트의 Unity가 실행 중입니다 (PID $($editor.ProcessId))."
        }
    }
}

function Assert-CleanCheckout {
    $top = Invoke-ReviewGit @('rev-parse', '--show-toplevel')
    if ([IO.Path]::GetFullPath($top) -ne [IO.Path]::GetFullPath($reviewPath)) {
        throw '검토 경로가 독립된 저장소가 아닙니다.'
    }

    $remote = Invoke-ReviewGit @('config', '--get', 'remote.origin.url')
    if ($remote -ne $repositoryUrl) { throw '검토 폴더의 원격 저장소가 다릅니다.' }

    $changes = Invoke-ReviewGit @('status', '--porcelain', '--untracked-files=all', '--ignore-submodules=none')
    if ($changes) {
        Write-Host ($changes -join "`n")
        throw '미커밋 또는 미추적 변경이 있습니다. 변경을 직접 확인한 뒤 다시 실행해 주세요.'
    }

    $branch = Invoke-ReviewGit @('branch', '--show-current')
    if ($branch) { throw '검토 폴더에 작업 브랜치가 있습니다. 자동 전환을 중단합니다.' }
}

foreach ($program in @('git', 'gh')) {
    if (-not (Get-Command $program -ErrorAction SilentlyContinue)) {
        throw "$program 명령이 필요합니다. 설치 및 GitHub 인증 후 다시 실행해 주세요."
    }
}

$prJson = Invoke-Checked gh @('pr', 'view', "$PrNumber", '--repo', $repository,
    '--json', 'number,title,state,headRefOid')
$pr = ($prJson -join "`n") | ConvertFrom-Json
if ($pr.state -ne 'OPEN' -or $pr.headRefOid -notmatch '^[0-9a-f]{40}$') {
    throw '열린 PR과 유효한 커밋만 검토 대상으로 지원합니다.'
}

Write-Host "PR #$PrNumber : $($pr.title)"
Write-Host "검토 커밋: $($pr.headRefOid)"
Write-Host "공용 프로젝트: $projectPath"
if ($CheckOnly) { return }

if (-not $PrepareOnly -and -not (Get-Command unity -ErrorAction SilentlyContinue)) {
    throw 'Unity CLI가 필요합니다. 설치 후 다시 실행하거나 -PrepareOnly를 사용해 주세요.'
}

# 다른 PR을 동시에 준비하면서 동일 checkout을 바꾸지 않도록 직렬화합니다.
$mutex = New-Object System.Threading.Mutex($false, 'Local\ProjectNomad.PRReview')
$ownsMutex = $false
try {
    try { $ownsMutex = $mutex.WaitOne(0) }
    catch [System.Threading.AbandonedMutexException] { $ownsMutex = $true }
    if (-not $ownsMutex) { throw '다른 PR 검토 준비가 실행 중입니다. 완료 후 다시 실행해 주세요.' }

    Assert-ProjectClosed
    if (Test-Path -LiteralPath $reviewPath) {
        Assert-CleanCheckout
        # 검토 폴더에서 만든 detached 커밋도 PR 전환 전에 로컬 참조로 보존합니다.
        $previousSha = Invoke-ReviewGit @('rev-parse', 'HEAD')
        Invoke-ReviewGit @('update-ref', "refs/review/history/$previousSha", $previousSha)
    } else {
        New-Item -ItemType Directory -Path (Split-Path $reviewPath) -Force | Out-Null
        Invoke-Checked git @('clone', '--no-checkout', $repositoryUrl, $reviewPath)
    }

    Invoke-ReviewGit @('fetch', 'origin', "refs/pull/$PrNumber/head")
    $fetchedSha = Invoke-ReviewGit @('rev-parse', 'FETCH_HEAD')
    if ($fetchedSha -ne $pr.headRefOid) {
        throw '조회 도중 PR이 갱신되었습니다. 다시 실행해 주세요.'
    }

    # PR이 실수로 캐시를 추적하더라도 기존 Library를 덮어쓰지 않습니다.
    $trackedCache = Invoke-ReviewGit @('ls-tree', '-r', '--name-only', $fetchedSha,
        '--', 'UnityProject/Library', 'UnityProject/Temp')
    if ($trackedCache) { throw 'PR에 Library 또는 Temp 파일이 추적되어 있어 전환을 중단합니다.' }

    $versionText = Invoke-ReviewGit @('show', "${fetchedSha}:UnityProject/ProjectSettings/ProjectVersion.txt")
    if (($versionText -join "`n") -notmatch '(?m)^m_EditorVersion:\s*(\S+)') {
        throw 'PR의 Unity 버전을 읽지 못했습니다.'
    }
    $editorVersion = $Matches[1]
    if ($editorVersion -notmatch '^\d+\.\d+\.\d+[abfp]\d+$') {
        throw '지원하지 않는 Unity 버전 형식입니다.'
    }

    $editorPath = Join-Path ${env:ProgramFiles} "Unity/Hub/Editor/$editorVersion/Editor/Unity.exe"
    if (-not $PrepareOnly -and -not (Test-Path -LiteralPath $editorPath)) {
        throw "Unity Hub에서 $editorVersion 버전을 설치해 주세요. 자동 설치나 업그레이드는 하지 않습니다."
    }

    Assert-ProjectClosed
    Invoke-ReviewGit @('update-ref', "refs/review/PR-$PrNumber/$fetchedSha", $fetchedSha)
    # --no-overwrite-ignore로 다른 PR이 추적하기 시작한 파일도 조용히 덮어쓰지 않습니다.
    Invoke-ReviewGit @('checkout', '--detach', '--no-overwrite-ignore', $fetchedSha)
    Invoke-ReviewGit @('submodule', 'sync', '--recursive')
    Invoke-ReviewGit @('submodule', 'update', '--init', '--recursive')

    Write-Host "준비 완료: PR #$PrNumber / $fetchedSha / Unity $editorVersion"
    Write-Host 'Library 캐시는 유지했습니다. 에셋·패키지·Unity 버전에 따라 재임포트는 발생할 수 있습니다.'
    if ($PrepareOnly) { return }

    Assert-ProjectClosed
    Invoke-Checked unity @('open', $projectPath, '--editor-path', $editorPath, '--non-interactive')
}
finally {
    if ($ownsMutex) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
