param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$pluginProject = Join-Path $root 'src\Plugin\ChipGuide.Plugin.csproj'
$projectXml = [xml](Get-Content -LiteralPath $pluginProject -Raw -Encoding UTF8)
$version = [string]$projectXml.Project.PropertyGroup.Version
$plugin = Get-Content -LiteralPath (Join-Path $root 'src\Plugin\Plugin.cs') -Raw -Encoding UTF8
if ($plugin -notmatch ('const string Version = "' + [regex]::Escape($version) + '";')) {
    throw "Plugin.cs의 Version과 프로젝트 버전($version)이 다릅니다."
}

$dist = Join-Path $root 'dist'
$zip = Join-Path $dist "AstralPartyChipGuide-v$version.zip"
if (Test-Path -LiteralPath $zip) { throw "이미 있는 패키지입니다. 지우고 다시 실행하세요: $zip" }

& dotnet build $pluginProject -c $Configuration
if ($LASTEXITCODE -ne 0) { throw '플러그인 빌드에 실패했습니다.' }
& dotnet build (Join-Path $root 'src\Updater\ChipGuide.Updater.csproj') -c $Configuration -p:Version=$version
if ($LASTEXITCODE -ne 0) { throw '확인 프로그램 빌드에 실패했습니다.' }

$bin = Join-Path $root "src\Plugin\bin\$Configuration\net6.0"
$updaterBin = Join-Path $root "src\Updater\bin\$Configuration\net48"
$files = @(
    @{ Source = (Join-Path $bin 'AstralPartyChipGuide.dll'); Name = 'AstralPartyChipGuide.dll' },
    @{ Source = (Join-Path $bin 'ChipGuide.Core.dll'); Name = 'ChipGuide.Core.dll' },
    @{ Source = (Join-Path $updaterBin 'AstralPartyChipGuideUpdater.exe'); Name = 'AstralPartyChipGuideUpdater.exe' },
    @{ Source = (Join-Path $updaterBin 'AstralPartyChipGuideUpdater.exe.config'); Name = 'AstralPartyChipGuideUpdater.exe.config' },
    @{ Source = (Join-Path $root 'README.md'); Name = 'README.md' },
    @{ Source = (Join-Path $root 'LICENSE'); Name = 'LICENSE' }
)
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file.Source)) { throw "패키지에 넣을 파일이 없습니다: $($file.Source)" }
}

New-Item -ItemType Directory -Force $dist | Out-Null
# Compress-Archive(PS 5.1)는 항목 경로에 역슬래시를 넣어 다른 압축 프로그램에서 폴더가 깨진다.
Add-Type -AssemblyName System.IO.Compression
$output = [IO.File]::Open($zip, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
try {
    $archive = New-Object IO.Compression.ZipArchive($output, [IO.Compression.ZipArchiveMode]::Create, $true)
    try {
        foreach ($file in $files) {
            # BepInEx 폴더에 그대로 풀면 plugins 아래 자리를 잡도록 plugins부터 경로를 넣는다.
            $entry = $archive.CreateEntry("plugins/AstralPartyChipGuide/$($file.Name)", [IO.Compression.CompressionLevel]::Optimal)
            $source = [IO.File]::OpenRead($file.Source)
            $target = $entry.Open()
            try { $source.CopyTo($target) }
            finally { $target.Dispose(); $source.Dispose() }
        }
    }
    finally { $archive.Dispose() }
}
finally { $output.Dispose() }

Write-Output $zip
