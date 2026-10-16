param(
    [string]$OutputDirectory = "artifacts"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
$projectPath = Join-Path $repositoryRoot "src\KeyStar.App\KeyStar.App.csproj"
$readmePath = Join-Path $repositoryRoot "docs\USER_GUIDE.md"
$imagesPath = Join-Path $repositoryRoot "docs\images"
$fullOutput = Join-Path $artifactsRoot "v1.0-full"
$liteOutput = Join-Path $artifactsRoot "v1.0-lite"

New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null

# 发布前把已有 Data 目录搬到临时区保管，避免清理输出目录时连同本地统计数据一起删掉，
# 发布完成后再原样搬回（用户可能就在 artifacts 目录里直接运行过程序）。
$preservedData = @{}
foreach ($target in @($fullOutput, $liteOutput)) {
    $resolvedTarget = [System.IO.Path]::GetFullPath($target)
    if (-not $resolvedTarget.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "拒绝清理 artifacts 目录以外的路径：$resolvedTarget"
    }

    $existingData = Join-Path $resolvedTarget "Data"
    if (Test-Path -LiteralPath $existingData) {
        $stash = Join-Path $artifactsRoot (".data-stash-" + [System.IO.Path]::GetFileName($resolvedTarget))
        if (Test-Path -LiteralPath $stash) {
            Remove-Item -LiteralPath $stash -Recurse -Force
        }

        Move-Item -LiteralPath $existingData -Destination $stash
        $preservedData[$resolvedTarget] = $stash
        Write-Host "已暂存本地数据：$existingData"
    }

    if (Test-Path -LiteralPath $resolvedTarget) {
        Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
    }
}

function Restore-PreservedData {
    param([string]$OutputPath)

    if (-not $preservedData.ContainsKey($OutputPath)) {
        return
    }

    $stash = $preservedData[$OutputPath]
    $destination = Join-Path $OutputPath "Data"
    if (Test-Path -LiteralPath $destination) {
        Remove-Item -LiteralPath $destination -Recurse -Force
    }

    Move-Item -LiteralPath $stash -Destination $destination
    Write-Host "已恢复本地数据：$destination"
}

dotnet publish $projectPath -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $fullOutput

New-Item -ItemType Directory -Path (Join-Path $fullOutput "Data") -Force | Out-Null
Copy-Item -LiteralPath $readmePath -Destination (Join-Path $fullOutput "README.md")
if (Test-Path -LiteralPath $imagesPath) {
    Copy-Item -LiteralPath $imagesPath -Destination (Join-Path $fullOutput "images") -Recurse
}

dotnet publish $projectPath -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $liteOutput

New-Item -ItemType Directory -Path (Join-Path $liteOutput "Data") -Force | Out-Null
Copy-Item -LiteralPath $readmePath -Destination (Join-Path $liteOutput "README.md")
if (Test-Path -LiteralPath $imagesPath) {
    Copy-Item -LiteralPath $imagesPath -Destination (Join-Path $liteOutput "images") -Recurse
}

$fullZip = Join-Path $artifactsRoot "KeyStar-V1.0-full-win-x64.zip"
$liteZip = Join-Path $artifactsRoot "KeyStar-V1.0-lite-win-x64.zip"
Compress-Archive -Path (Join-Path $fullOutput "*") -DestinationPath $fullZip -CompressionLevel Optimal -Force
Compress-Archive -Path (Join-Path $liteOutput "*") -DestinationPath $liteZip -CompressionLevel Optimal -Force

$checksums = @($fullZip, $liteZip) | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    "$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($_))"
}
[System.IO.File]::WriteAllLines((Join-Path $artifactsRoot "SHA256SUMS.txt"), $checksums)

# 打包完成后才把本地数据搬回输出目录，确保个人统计数据不会进入发行包。
Restore-PreservedData -OutputPath $fullOutput
Restore-PreservedData -OutputPath $liteOutput

Write-Host "发布完成：$artifactsRoot"

