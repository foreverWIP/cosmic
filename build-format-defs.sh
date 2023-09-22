#!/bin/sh

for file in ./Cosmic.Formats/defs/*.ksy; do
    ./kaitai-struct-compiler-0.10/bin/kaitai-struct-compiler.bat -t csharp --outdir ./Cosmic.Formats/gen --dotnet-namespace Cosmic.Formats -I ./Cosmic.Core/Formats/gen $file.FullName
done