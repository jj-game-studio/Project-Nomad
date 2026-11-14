#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$launcher = Join-Path $PSScriptRoot 'Open-PrInUnity.ps1'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('nomad-pr-review-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $fixture 'source'
$originalEnvironment = @{}
foreach ($name in @('LOCALAPPDATA', 'ProgramFiles', 'GIT_CONFIG_COUNT', 'GIT_CONFIG_KEY_0', 'GIT_CONFIG_VALUE_0')) {
    $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
}

function Run-Git {
    & git @args
    if ($LASTEXITCODE -ne 0) { throw "fixture git 실패: $args" }
}

function Assert-Equal($actual, $expected, $message) {
    if ($actual -ne $expected) { throw "$message : $actual != $expected" }
}

function Expect-Blocked([scriptblock]$action, [string]$message) {
    try { & $action } catch {
        if ($_.Exception.Message -notlike "*$message*") { throw }
        Write-Host "PASS: $message"
        return
    }
    throw "차단되지 않았습니다: $message"
}

# GitHub/Unity/프로세스 조회만 대체하고 clone·fetch·checkout은 실제 Git을 실행합니다.
function gh {
    $number = [int]$args[2]
    $global:LASTEXITCODE = 0
    @{ number = $number; title = "fixture $number"; state = 'OPEN'; headRefOid = $global:NomadPrReviewFixtureHeads[$number] } |
        ConvertTo-Json -Compress
}

function unity {
    $global:NomadPrReviewUnityArguments = @($args)
    $global:LASTEXITCODE = 0
}

function Get-CimInstance { return @() }

try {
    New-Item -ItemType Directory -Path "$source/UnityProject/ProjectSettings" -Force | Out-Null
    Set-Content "$source/.gitignore" "Library/`nTemp/`nignored.txt"
    Set-Content "$source/UnityProject/ProjectSettings/ProjectVersion.txt" 'm_EditorVersion: 6000.6.9f1'
    Run-Git init $source
    Run-Git -C $source config user.name 'PR review fixture'
    Run-Git -C $source config user.email 'fixture@example.invalid'
    Run-Git -C $source add .
    Run-Git -C $source commit -m first
    $first = Run-Git -C $source rev-parse HEAD
    Set-Content "$source/UnityProject/change.txt" 'second PR'
    Run-Git -C $source add .
    Run-Git -C $source commit -m second
    $second = Run-Git -C $source rev-parse HEAD
    Run-Git -C $source update-ref refs/pull/1/head $first
    Run-Git -C $source update-ref refs/pull/2/head $second
    $global:NomadPrReviewFixtureHeads = @{ 1 = $first; 2 = $second }

    $env:LOCALAPPDATA = Join-Path $fixture 'local app data'
    $env:ProgramFiles = Join-Path $fixture 'program files'
    $env:GIT_CONFIG_COUNT = '1'
    $env:GIT_CONFIG_KEY_0 = "url.$($source.Replace('\', '/')).insteadOf"
    $env:GIT_CONFIG_VALUE_0 = 'https://github.com/jj-game-studio/Project-Nomad.git'
    $checkout = Join-Path $env:LOCALAPPDATA 'ProjectNomad/PRReview'
    $project = Join-Path $checkout 'UnityProject'

    & $launcher -PrNumber 1 -CheckOnly
    Assert-Equal (Test-Path $checkout) $false '조회는 폴더를 만들지 않아야 합니다'
    & $launcher -PrNumber 1 -PrepareOnly
    Assert-Equal (Run-Git -C $checkout rev-parse HEAD) $first '최초 PR HEAD'

    New-Item -ItemType Directory "$project/Library" | Out-Null
    $cache = Join-Path $project 'Library/cache-sentinel'
    Set-Content $cache 'preserve this cache'
    $cacheHash = (Get-FileHash $cache).Hash
    & $launcher -PrNumber 2 -PrepareOnly
    Assert-Equal (Run-Git -C $checkout rev-parse HEAD) $second '다른 PR 전환'
    Assert-Equal (Get-FileHash $cache).Hash $cacheHash '전환 후 캐시 보존'
    & $launcher -PrNumber 2 -PrepareOnly
    Assert-Equal (Get-FileHash $cache).Hash $cacheHash '동일 PR 재사용'
    Write-Host 'PASS: 최초 준비 / PR 전환 / 재사용 / 캐시 보존'

    Set-Content "$project/change.txt" 'local edit'
    Expect-Blocked { & $launcher -PrNumber 1 -PrepareOnly } '미커밋'
    Assert-Equal (Get-Content "$project/change.txt") 'local edit' '수정 파일 보존'
    Run-Git -C $checkout restore UnityProject/change.txt
    Set-Content "$project/untracked.txt" 'keep'
    Expect-Blocked { & $launcher -PrNumber 1 -PrepareOnly } '미추적'
    Remove-Item -LiteralPath "$project/untracked.txt"

    New-Item -ItemType Directory "$project/Temp" | Out-Null
    Set-Content "$project/Temp/UnityLockfile" 'fixture lock'
    Expect-Blocked { & $launcher -PrNumber 1 -PrepareOnly } '잠금 파일'
    Remove-Item -LiteralPath "$project/Temp/UnityLockfile"
    Run-Git -C $checkout switch -c fixture-work
    Expect-Blocked { & $launcher -PrNumber 1 -PrepareOnly } '작업 브랜치'
    Run-Git -C $checkout checkout --detach

    # 임의의 detached 커밋도 전환 후 참조를 통해 살아 있어야 합니다.
    Run-Git -C $checkout -c user.name=fixture -c user.email=fixture@example.invalid commit --allow-empty -m local
    $localCommit = Run-Git -C $checkout rev-parse HEAD
    & $launcher -PrNumber 1 -PrepareOnly
    Assert-Equal (Run-Git -C $checkout rev-parse "refs/review/history/$localCommit") $localCommit '로컬 커밋 보존'

    $fakeEditor = Join-Path $env:ProgramFiles 'Unity/Hub/Editor/6000.6.9f1/Editor/Unity.exe'
    New-Item -ItemType Directory (Split-Path $fakeEditor) -Force | Out-Null
    New-Item -ItemType File $fakeEditor | Out-Null
    & $launcher -PrNumber 1
    Assert-Equal $global:NomadPrReviewUnityArguments[0] 'open' 'Unity open 호출'
    Assert-Equal $global:NomadPrReviewUnityArguments[1] $project '공백 포함 프로젝트 경로'
    Assert-Equal $global:NomadPrReviewUnityArguments[3] $fakeEditor '지정 버전 Editor 경로'
    Write-Host 'PASS: 커밋 보존 / Unity 호출 인수'

    # 캐시를 추적하는 PR은 checkout 전에 거절해야 합니다.
    Run-Git -C $source add -f UnityProject/ProjectSettings/ProjectVersion.txt
    New-Item -ItemType Directory "$source/UnityProject/Library" -Force | Out-Null
    Set-Content "$source/UnityProject/Library/cache-sentinel" 'overwrite attempt'
    Run-Git -C $source add -f UnityProject/Library/cache-sentinel
    Run-Git -C $source commit -m tracked-cache
    $third = Run-Git -C $source rev-parse HEAD
    Run-Git -C $source update-ref refs/pull/3/head $third
    $global:NomadPrReviewFixtureHeads[3] = $third
    Expect-Blocked { & $launcher -PrNumber 3 -PrepareOnly } 'Library 또는 Temp'
    Assert-Equal (Get-FileHash $cache).Hash $cacheHash '차단 후 캐시 보존'
    Assert-Equal (Run-Git -C $checkout rev-parse HEAD) $first '차단 후 HEAD 보존'

    $global:NomadPrReviewFixtureHeads[2] = $first
    Expect-Blocked { & $launcher -PrNumber 2 -PrepareOnly } 'PR이 갱신'
    Write-Host "전체 통과. fixture 보존: $fixture"
}
finally {
    foreach ($name in $originalEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name])
    }
}
