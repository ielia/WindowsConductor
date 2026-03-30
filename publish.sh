#!/usr/bin/env bash
set -euo pipefail

CONFIG=Release
RUNTIME=win-x64
OUTDIR=publish

PROJECTS=(WindowsConductor.DriverFlaUI WindowsConductor.InspectorGUI)
NUGET_PACKAGES=(WindowsConductor.Client)

echo "Publishing framework-dependent builds..."
for p in "${PROJECTS[@]}"; do
    name="${p#WindowsConductor.}"
    dotnet publish "$p" -c "$CONFIG" -o "$OUTDIR/$name/framework-dependent"
done

echo "Publishing self-contained builds..."
for p in "${PROJECTS[@]}"; do
    name="${p#WindowsConductor.}"
    dotnet publish "$p" -c "$CONFIG" --self-contained -r "$RUNTIME" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$OUTDIR/$name/self-contained"
done

echo "Packing NuGet packages..."
for p in "${NUGET_PACKAGES[@]}"; do
    name="${p#WindowsConductor.}"
    dotnet pack "$p" -c "$CONFIG" -o "$OUTDIR/$name/NuGet"
done

echo "Done. Output in $OUTDIR/"
