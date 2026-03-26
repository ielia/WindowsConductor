@echo off
set PORT=%1
if "%PORT%"=="" set PORT=8765
dotnet run --project WindowsConductor.DriverFlaUI -- "http://localhost:%PORT%/"
