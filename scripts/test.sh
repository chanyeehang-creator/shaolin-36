#!/usr/bin/env bash
# Runs every check Claude Code runs: data check and the core rule tests.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet run --project tools/DataCheck -- game/data
dotnet test Shaolin36.sln -nologo
