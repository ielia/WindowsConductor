$Config  = 'Release'
$Runtime = 'win-x64'
$OutDir  = 'publish'

$projects = @('WindowsConductor.DriverFlaUI', 'WindowsConductor.InspectorGUI')
$nugetPackages = @('WindowsConductor.Client')

Write-Host 'Publishing framework-dependent builds...'
foreach ($p in $projects) {
    $name = $p -replace 'WindowsConductor\.', ''
    dotnet publish $p -c $Config -o "$OutDir\$name\framework-dependent"
}

Write-Host 'Publishing self-contained builds...'
foreach ($p in $projects) {
    $name = $p -replace 'WindowsConductor\.', ''
    dotnet publish $p -c $Config --self-contained -r $Runtime -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$OutDir\$name\self-contained"
}

Write-Host 'Packing NuGet packages...'
foreach ($p in $nugetPackages) {
    $name = $p -replace 'WindowsConductor\.', ''
    dotnet pack $p -c $Config -o "$OutDir\$name\NuGet"
}

Write-Host "Done. Output in $OutDir\"
