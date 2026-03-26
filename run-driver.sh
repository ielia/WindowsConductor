#!/usr/bin/env bash
PORT="${1:-8765}"
dotnet run --project WindowsConductor.DriverFlaUI -- "http://localhost:$PORT/"
