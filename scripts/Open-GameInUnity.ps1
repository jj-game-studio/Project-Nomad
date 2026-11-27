#Requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$CheckOnly,
    [switch]$InstallShortcut
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $env:LOCALAPPDATA) { throw 'LOCALAPPDATA 환경 변수가 필요합니다.' }

# 개발 작업본을 직접 열지 않고, 사람이 확인하는 프로젝트 경로를 고정합니다.
$reviewPath = Join-Path $env:LOCALAPPDATA 'ProjectNomad/PRReview'
$projectPath = Join-Path $reviewPath 'UnityProject'
$scenePath = Join-Path $projectPath 'Assets/Nomad/Game/Game.unity'
$versionPath = Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionPath) -or -not (Test-Path -LiteralPath $scenePath)) {
    throw '공용 플레이 프로젝트가 준비되지 않았습니다. Open-PrInUnity.ps1 -PrNumber <번호> -PrepareOnly로 준비해 주세요.'
}

$versionText = Get-Content -LiteralPath $versionPath -Raw
if ($versionText -notmatch '(?m)^m_EditorVersion:\s*(\d+\.\d+\.\d+[abfp]\d+)\s*$') {
    throw '공용 프로젝트의 Unity 버전을 읽지 못했습니다.'
}
$editorPath = Join-Path ${env:ProgramFiles} "Unity/Hub/Editor/$($Matches[1])/Editor/Unity.exe"
if (-not (Test-Path -LiteralPath $editorPath)) { throw "Unity Editor가 없습니다: $editorPath" }

$alreadyOpen = Test-Path -LiteralPath (Join-Path $projectPath 'Temp/UnityLockfile')
foreach ($editor in (Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'")) {
    if ($editor.CommandLine -and $editor.CommandLine.Replace('\', '/').IndexOf(
        $projectPath.Replace('\', '/'), [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        $alreadyOpen = $true
    }
}

if ($CheckOnly) {
    [pscustomobject]@{ ProjectPath = $projectPath; ScenePath = $scenePath; EditorPath = $editorPath; AlreadyOpen = $alreadyOpen }
    return
}

if ($InstallShortcut) {
    $launcherPath = Join-Path $env:LOCALAPPDATA 'ProjectNomad/Open-ProjectNomad.ps1'
    if ([IO.Path]::GetFullPath($PSCommandPath) -ne [IO.Path]::GetFullPath($launcherPath)) {
        Copy-Item -LiteralPath $PSCommandPath -Destination $launcherPath -Force
    }
    $desktop = [Environment]::GetFolderPath('Desktop')
    $shortcutPath = Join-Path $desktop 'Project Nomad.lnk'
    if (Test-Path -LiteralPath $shortcutPath) {
        Copy-Item -LiteralPath $shortcutPath -Destination ($shortcutPath + '.backup') -Force
    }

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = (Get-Process -Id $PID).Path
    $shortcut.Arguments = '-NoProfile -File "' + $launcherPath + '"'
    $shortcut.WorkingDirectory = $reviewPath
    $shortcut.IconLocation = "$editorPath,0"
    $shortcut.Description = 'Project Nomad 공용 플레이 프로젝트'
    $shortcut.Save()
    Write-Host "바로가기: $shortcutPath"
    return
}

if ($alreadyOpen) {
    Write-Host "Project Nomad가 이미 열려 있거나 잠겨 있습니다: $projectPath"
    return
}

# 재실행할 때 이전 디버그 씬 대신 통합 게임의 시작 씬을 엽니다.
Start-Process -FilePath $editorPath -WorkingDirectory $projectPath -ArgumentList @(
    '-projectPath', ('"' + $projectPath + '"'),
    '-executeMethod', 'Nomad.Game.Editor.GameSceneAuthoring.Open'
)
Write-Host "Project Nomad 실행 요청: $projectPath (Game 씬)"
