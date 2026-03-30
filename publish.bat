@echo off
setlocal

set CONFIG=Release
set RUNTIME=win-x64
set OUTDIR=publish

echo Publishing framework-dependent builds...
dotnet publish WindowsConductor.DriverFlaUI -c %CONFIG% -o %OUTDIR%\DriverFlaUI\framework-dependent
dotnet publish WindowsConductor.InspectorGUI -c %CONFIG% -o %OUTDIR%\InspectorGUI\framework-dependent

echo Publishing self-contained builds...
dotnet publish WindowsConductor.DriverFlaUI -c %CONFIG% --self-contained -r %RUNTIME% -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o %OUTDIR%\DriverFlaUI\self-contained
dotnet publish WindowsConductor.InspectorGUI -c %CONFIG% --self-contained -r %RUNTIME% -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o %OUTDIR%\InspectorGUI\self-contained

echo Packing NuGet package...
dotnet pack WindowsConductor.Client -c %CONFIG% -o %OUTDIR%\Client\NuGet

echo Done. Output in %OUTDIR%\
