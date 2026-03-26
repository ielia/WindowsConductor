@echo off
pushd "%~dp0"

if exist coverage-results rd /s /q coverage-results
if exist coverage-report rd /s /q coverage-report

dotnet test --filter "TestCategory=Unit" --collect:"XPlat Code Coverage" --results-directory coverage-results
if errorlevel 1 goto :end

"%USERPROFILE%\.dotnet\tools\reportgenerator.exe" -reports:"coverage-results\*\coverage.cobertura.xml" -targetdir:coverage-report -reporttypes:"TextSummary;Html"
type coverage-report\Summary.txt

:end
popd
