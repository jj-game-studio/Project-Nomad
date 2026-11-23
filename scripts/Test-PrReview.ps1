#Requires -Version 7.0
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$launcher = Join-Path $PSScriptRoot 'Open-PrInUnity.ps1'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('nomad-pr-review-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $fixture 'source'
$assetSource = Join-Path $fixture 'assets'
$originalEnvironment = @{}
foreach ($name in @('LOCALAPPDATA', 'ProgramFiles', 'GIT_CONFIG_COUNT', 'GIT_CONFIG_KEY_0', 'GIT_CONFIG_VALUE_0',
        'GIT_CONFIG_KEY_1', 'GIT_CONFIG_VALUE_1')) {
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
    Run-Git init $assetSource
    Set-Content "$assetSource/model.txt" 'asset commit'
    Run-Git -C $assetSource add model.txt
    Run-Git -C $assetSource -c user.name=fixture -c user.email=fixture@example.invalid commit -m assets
    $assetHead = Run-Git -C $assetSource rev-parse HEAD

    New-Item -ItemType Directory -Path "$source/UnityProject/ProjectSettings" -Force | Out-Null
    Set-Content "$source/.gitignore" "Library/`nTemp/`nignored.txt"
    Set-Content "$source/UnityProject/ProjectSettings/ProjectVersion.txt" 'm_EditorVersion: 6000.6.9f1'
    Run-Git init $source
    Run-Git -C $source config user.name 'PR review fixture'
    Run-Git -C $source config user.email 'fixture@example.invalid'
    Run-Git -C $source -c protocol.file.allow=always submodule add $assetSource UnityProject/Assets/ThirdParty
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
    $env:GIT_CONFIG_COUNT = '2'
    $env:GIT_CONFIG_KEY_0 = "url.$($source.Replace('\', '/')).insteadOf"
    $env:GIT_CONFIG_VALUE_0 = 'https://github.com/jj-game-studio/Project-Nomad.git'
    $env:GIT_CONFIG_KEY_1 = 'protocol.file.allow'
    $env:GIT_CONFIG_VALUE_1 = 'always'
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
    Set-Content "$project/staged.txt" 'staged addition'
    Run-Git -C $checkout add UnityProject/staged.txt
    Set-Content "$project/untracked.txt" 'temporary'
    $outside = Join-Path $fixture 'outside-review'
    New-Item -ItemType Directory -Path $outside | Out-Null
    Set-Content "$outside/keep.txt" 'outside file'
    New-Item -ItemType Junction -Path "$project/linked-folder" -Target $outside | Out-Null
    $module = Join-Path $project 'Assets/ThirdParty'
    Set-Content "$module/model.txt" 'local asset edit'
    Run-Git -C $module add model.txt
    Set-Content "$module/untracked.txt" 'temporary asset'

    & $launcher -PrNumber 2 -CheckOnly
    Assert-Equal (Get-Content "$project/change.txt") 'local edit' '조회 시 수정 보존'
    Assert-Equal (Test-Path "$module/untracked.txt") $true '조회 시 서브모듈 수정 보존'
    & $launcher -PrNumber 2 -PrepareOnly
    Assert-Equal (Get-Content "$project/change.txt") 'second PR' '같은 PR의 수정 폐기'
    Assert-Equal (Test-Path "$project/staged.txt") $false '스테이징 파일 정리'
    Assert-Equal (Test-Path "$project/untracked.txt") $false '미추적 파일 정리'
    Assert-Equal (Get-Content "$module/model.txt") 'asset commit' '서브모듈 스테이징 변경 폐기'
    Assert-Equal (Test-Path "$module/untracked.txt") $false '서브모듈 미추적 파일 정리'
    Assert-Equal @(Run-Git -C $checkout status --porcelain).Count 0 'Git 상태 정리'
    Assert-Equal (Get-Content "$outside/keep.txt") 'outside file' '정리 범위 밖의 파일 보존'
    Assert-Equal (Get-FileHash $cache).Hash $cacheHash '정리 후 Library 보존'

    Run-Git -C $checkout update-index --cacheinfo "160000,$second,UnityProject/Assets/ThirdParty"
    & $launcher -PrNumber 2 -PrepareOnly
    Assert-Equal (Run-Git -C $checkout rev-parse ':UnityProject/Assets/ThirdParty') $assetHead '스테이징된 서브모듈 포인터 복원'

    Set-Content "$project/change.txt" 'replace without carrying'
    & $launcher -PrNumber 1 -PrepareOnly
    Assert-Equal (Test-Path "$project/change.txt") $false '다른 PR로 전환 시 수정 폐기'
    Set-Content "$project/change.txt" 'untracked collision'
    & $launcher -PrNumber 2 -PrepareOnly
    Assert-Equal (Get-Content "$project/change.txt") 'second PR' '새 PR과 충돌하는 미추적 파일 정리'
    Write-Host 'PASS: 변경 및 서브모듈 폐기 / 같은 PR 재준비 / 충돌 파일 정리'

    New-Item -ItemType Directory "$project/Temp" | Out-Null
    Set-Content "$project/Temp/UnityLockfile" 'fixture lock'
    Set-Content "$project/change.txt" 'local before lock'
    Expect-Blocked { & $launcher -PrNumber 1 -PrepareOnly } '잠금 파일'
    Assert-Equal (Get-Content "$project/change.txt") 'local before lock' '잠금 중 변경 보존'
    Remove-Item -LiteralPath "$project/Temp/UnityLockfile"
    Run-Git -C $checkout switch -c fixture-work
    Expect-Blocked { & $launcher -PrNumber 1 -PrepareOnly } '작업 브랜치'
    Assert-Equal (Get-Content "$project/change.txt") 'local before lock' '작업 브랜치 변경 보존'
    Run-Git -C $checkout checkout --detach

    # 과거 로컬 커밋에 등록되지 않은 gitlink나 혼합 줄바꿈 파일이 있어도 전환합니다.
    Run-Git -C $checkout update-index --add --cacheinfo "160000,$assetHead,old-link"
    $mixedScene = Join-Path $project 'mixed.unity'
    [IO.File]::WriteAllBytes($mixedScene, [Text.Encoding]::ASCII.GetBytes("first`nsecond`r`n"))
    Run-Git -C $checkout -c core.autocrlf=false add UnityProject/mixed.unity
    Set-Content "$checkout/.gitattributes" '*.unity text eol=lf'
    Run-Git -C $checkout add .gitattributes
    Run-Git -C $checkout -c user.name=fixture -c user.email=fixture@example.invalid commit -m local
    $localCommit = Run-Git -C $checkout rev-parse HEAD
    & $launcher -PrNumber 1 -PrepareOnly
    Assert-Equal (Run-Git -C $checkout rev-parse "refs/review/history/$localCommit") $localCommit '로컬 커밋 보존'
    Assert-Equal (Test-Path -LiteralPath $mixedScene) $false '혼합 줄바꿈이 있는 과거 커밋에서 전환'

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
    Set-Content "$project/untracked.txt" 'keep if target is invalid'
    Expect-Blocked { & $launcher -PrNumber 3 -PrepareOnly } 'Library 또는 Temp'
    Assert-Equal (Get-FileHash $cache).Hash $cacheHash '차단 후 캐시 보존'
    Assert-Equal (Run-Git -C $checkout rev-parse HEAD) $first '차단 후 HEAD 보존'
    Assert-Equal (Get-Content "$project/untracked.txt") 'keep if target is invalid' '차단 후 수정 파일 보존'

    $global:NomadPrReviewFixtureHeads[2] = $first
    Expect-Blocked { & $launcher -PrNumber 2 -PrepareOnly } 'PR이 갱신'
    Write-Host "전체 통과. fixture 보존: $fixture"
}
finally {
    foreach ($name in $originalEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name])
    }
}
