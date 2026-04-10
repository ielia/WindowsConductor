@echo off
set PORT=%1
if "%PORT%"=="" set PORT=8765
shift

set EXTRA_ARGS=
:parse
if "%~1"=="" goto run
set EXTRA_ARGS=%EXTRA_ARGS% %1
if "%~1"=="--ffmpeg-path" (
    shift
    set EXTRA_ARGS=%EXTRA_ARGS% %1
)
if "%~1"=="--auth-token" (
    shift
    set EXTRA_ARGS=%EXTRA_ARGS% %1
)
if "%~1"=="--auth-token-file" (
    shift
    set EXTRA_ARGS=%EXTRA_ARGS% %1
)
if "%~1"=="--hash-token" (
    shift
    set EXTRA_ARGS=%EXTRA_ARGS% %1
)
if "%~1"=="--hash-token-file" (
    shift
    set EXTRA_ARGS=%EXTRA_ARGS% %1
)
shift
goto parse

:run
dotnet run --project WindowsConductor.DriverFlaUI -- "http://localhost:%PORT%/" %EXTRA_ARGS%
