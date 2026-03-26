$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot

if (Test-Path coverage-results) { Remove-Item coverage-results -Recurse -Force }
if (Test-Path coverage-report)  { Remove-Item coverage-report  -Recurse -Force }

dotnet test --filter "TestCategory=Unit" --collect:"XPlat Code Coverage" --results-directory coverage-results
reportgenerator -reports:"coverage-results/*/coverage.cobertura.xml" -targetdir:coverage-report -reporttypes:"TextSummary;Html"
Get-Content coverage-report/Summary.txt

Pop-Location
