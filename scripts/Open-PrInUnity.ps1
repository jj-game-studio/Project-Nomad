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

$reviewPath = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'ProjectNomad/PRReview'))
$projectPath = Join-Path $reviewPath 'UnityProject'
$reviewPrefix = $reviewPath + [IO.Path]::DirectorySeparatorChar
$cachePaths = @('UnityProject/Library', 'UnityProject/Temp')

function Invoke-Checked {
    param([string]$Program, [string[]]$Arguments)

    $output = & $Program @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Program 실행 실패 (종료 코드 $LASTEXITCODE). 기존 파일은 자동 삭제하지 않습니다."
    }

    return $output
}

function Invoke-ReviewGit {
    param([string[]]$Arguments, [string]$Path = $reviewPath)

    Invoke-Checked git (@('-c', 'core.hooksPath=NUL', '-C', $Path) + $Arguments)
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

function Assert-ReviewPath([string]$Path) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    if ($fullPath -ne $reviewPath -and -not $fullPath.StartsWith($reviewPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "공용 검토 폴더 밖의 경로입니다: $fullPath"
    }

    for ($item = Get-Item -LiteralPath $fullPath -Force; $null -ne $item; $item = $item.Parent) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "연결된 경로에서는 검토 변경을 폐기하지 않습니다: $($item.FullName)"
        }
    }
}

function Assert-ReviewRepository([string]$Path) {
    Assert-ReviewPath $Path
    $top = Invoke-ReviewGit @('rev-parse', '--show-toplevel') $Path
    if ([IO.Path]::GetFullPath($top) -ne [IO.Path]::GetFullPath($Path)) {
        throw "독립된 검토 저장소가 아닙니다: $Path"
    }

    $gitDir = [IO.Path]::GetFullPath((Invoke-ReviewGit @('rev-parse', '--absolute-git-dir') $Path))
    Assert-ReviewPath $gitDir
    $mainGitDir = Join-Path $reviewPath '.git'
    $modulePrefix = (Join-Path $mainGitDir 'modules') + [IO.Path]::DirectorySeparatorChar
    if (($Path -eq $reviewPath -and $gitDir -ne $mainGitDir) -or
        ($Path -ne $reviewPath -and -not $gitDir.StartsWith($modulePrefix, [StringComparison]::OrdinalIgnoreCase))) {
        throw "공용 검토 폴더가 소유한 Git 저장 경로가 아닙니다: $gitDir"
    }

    $branch = Invoke-ReviewGit @('branch', '--show-current') $Path
    if ($branch) { throw "작업 브랜치가 있는 검토 저장소입니다: $Path ($branch)" }
}

function Clear-ReviewChanges([string]$Path) {
    $changes = Invoke-ReviewGit @('status', '--porcelain', '--untracked-files=normal', '--ignore-submodules=dirty') $Path
    if (-not $changes) { return }

    Write-Host "검토용 미커밋 변경 폐기: $Path"
    Invoke-ReviewGit @('restore', '--source=HEAD', '--staged', '--worktree',
        '--no-recurse-submodules', '--ignore-skip-worktree-bits', '--', '.') $Path
    Invoke-ReviewGit @('clean', '-fd', '-e', 'Library/', '-e', 'Temp/', '--', '.') $Path
}

function Clear-ReviewRepository([string]$Path) {
    Clear-ReviewChanges $Path
    if (-not (Test-Path -LiteralPath (Join-Path $Path '.gitmodules') -PathType Leaf)) { return }

    $parentPrefix = $Path + [IO.Path]::DirectorySeparatorChar
    $moduleConfig = Invoke-ReviewGit @('config', '--file', '.gitmodules', '--list') $Path
    foreach ($entry in $moduleConfig) {
        if ($entry -notmatch '^submodule\..+\.path=(.+)$') { continue }

        $module = [IO.Path]::GetFullPath((Join-Path $Path $Matches[1]))
        if (-not $module.StartsWith($parentPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw "서브모듈 경로가 상위 검토 저장소 밖입니다: $module"
        }

        # 이전 커밋의 등록되지 않은 gitlink와 초기화되지 않은 모듈은 정리 대상이 아닙니다.
        if (Test-Path -LiteralPath (Join-Path $module '.git')) {
            Assert-ReviewRepository $module
            Clear-ReviewRepository $module
        }
    }
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
    $reuseCheckout = Test-Path -LiteralPath $reviewPath
    if ($reuseCheckout) {
        Assert-ReviewRepository $reviewPath
        if (Test-Path -LiteralPath $projectPath) { Assert-ReviewPath $projectPath }
        $remote = Invoke-ReviewGit @('config', '--get', 'remote.origin.url')
        if ($remote -ne $repositoryUrl) { throw '검토 폴더의 원격 저장소가 다릅니다.' }
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
    $trackedCache = Invoke-ReviewGit (@('ls-tree', '-r', '--name-only', $fetchedSha, '--') + $cachePaths)
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
    if ($reuseCheckout) {
        $oldCache = Invoke-ReviewGit (@('ls-tree', '-r', '--name-only', 'HEAD', '--') + $cachePaths)
        $indexedCache = Invoke-ReviewGit (@('ls-files', '--') + $cachePaths)
        if ($oldCache -or $indexedCache) { throw '현재 검토 checkout에 Library 또는 Temp 파일이 추적되어 있습니다.' }

        Clear-ReviewRepository $reviewPath
    }

    Invoke-ReviewGit @('update-ref', "refs/review/PR-$PrNumber/$fetchedSha", $fetchedSha)
    # 혼합 줄바꿈이 추적된 과거 커밋은 복원 직후에도 Git이 변경으로 인식할 수 있습니다.
    Invoke-ReviewGit @('checkout', '--force', '--detach', '--no-recurse-submodules', '--no-overwrite-ignore', $fetchedSha)
    Invoke-ReviewGit @('submodule', 'sync', '--recursive')
    Invoke-ReviewGit @('submodule', 'update', '--init', '--recursive', '--checkout')

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
