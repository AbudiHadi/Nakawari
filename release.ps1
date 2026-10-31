# Builds and publishes a Nakawari release to GitHub with Velopack.
#   .\release.ps1 2.1.0            pack + upload as a published release
#   .\release.ps1 2.1.0 -LocalOnly pack only (Setup.exe in build\releases)
# Needs: vpk (dotnet tool install -g vpk --version 1.2.161) and gh signed in to AbudiHadi.
param(
    [Parameter(Mandatory)][string]$Version,
    [switch]$LocalOnly
)
$ErrorActionPreference = 'Stop'
$repo = 'https://github.com/AbudiHadi/Nakawari'
$root = $PSScriptRoot
$publish = Join-Path $root 'build\publish'
$releases = Join-Path $root 'build\releases'

Remove-Item $publish -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish "$root\src\Nakawari.csproj" -c Release -r win-x64 --self-contained true -o $publish `
    -p:Version=$Version -p:DebugType=none
if ($LASTEXITCODE) { throw 'dotnet publish failed' }

$token = (gh auth token).Trim()
if (-not $LocalOnly) {
    # Previous release lets vpk build a small delta update next to the full package.
    vpk download github --repoUrl $repo --token $token -o $releases
}

vpk pack --packId Nakawari --packVersion $Version --packDir $publish --mainExe Nakawari.exe `
    --packTitle Nakawari --packAuthors AbudiHadi --icon "$root\src\Assets\nakawari.ico" --outputDir $releases
if ($LASTEXITCODE) { throw 'vpk pack failed' }

if (-not $LocalOnly) {
    vpk upload github --repoUrl $repo --token $token --outputDir $releases --publish `
        --releaseName "Nakawari $Version" --tag "v$Version"
    if ($LASTEXITCODE) { throw 'vpk upload failed' }
}
Get-ChildItem $releases | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
