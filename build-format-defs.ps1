Write-Output "Building format definitions..."
$ksyFiles = Get-ChildItem -Path "Cosmic.Formats/defs" -Filter *.ksy
foreach ($file in $ksyFiles) {
    ./kaitai-struct-compiler-0.10/bin/kaitai-struct-compiler.bat -t csharp --outdir ./Cosmic.Formats/gen --dotnet-namespace Cosmic.Formats -I ./Cosmic.Core/Formats/gen $file.FullName
}