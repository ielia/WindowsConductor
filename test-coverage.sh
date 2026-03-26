#!/usr/bin/env bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

rm -rf coverage-results coverage-report
dotnet test --filter "TestCategory=Unit" --collect:"XPlat Code Coverage" --results-directory coverage-results
~/.dotnet/tools/reportgenerator -reports:"coverage-results/*/coverage.cobertura.xml" -targetdir:coverage-report -reporttypes:"TextSummary;Html"
cat coverage-report/Summary.txt
