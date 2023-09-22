Write-Output "Building engine..."
$publishDir = "./Cosmic.Desktop/bin/Release/net8.0/win-x64/publish"
if (Test-Path -Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
dotnet clean -c Release
dotnet publish -c Release -r win-x64 -f net8.0 --nologo -verbosity:minimal -m --self-contained
# Write-Output "Compressing files..."
# ./upx-4.0.2-win64/upx.exe --ultra-brute -k $publishDir/Cosmic.Desktop.exe
#$dllsToPack = Get-ChildItem -Path $publishDir -Filter *.dll
#foreach ($file in $dllsToPack) {
#    ./upx-4.0.2-win64/upx.exe --ultra-brute -k $file.FullName
#}
# Write-Output "Copying Sonic CD files over..."
# Copy-Item -Recurse ./Scripts/ $publishDir
# Copy-Item -Recurse ./Data/ $publishDir
# Copy-Item -Recurse ./videos/ $publishDir