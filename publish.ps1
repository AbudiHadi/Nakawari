# Builds one self-contained Nakawari.exe (no .NET install needed) into .\publish
$ErrorActionPreference = 'Stop'
dotnet publish "$PSScriptRoot\src\Nakawari.csproj" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -o "$PSScriptRoot\publish"
Get-FileHash "$PSScriptRoot\publish\Nakawari.exe" -Algorithm SHA256 | Format-List Hash, Path
