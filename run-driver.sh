#!/usr/bin/env bash
PORT="${1:-8765}"
shift 2>/dev/null
dotnet run --project WindowsConductor.DriverFlaUI -- "http://localhost:$PORT/" "$@"
