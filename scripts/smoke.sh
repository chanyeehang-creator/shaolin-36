#!/usr/bin/env bash
# Headless boot check: imports the project, boots the main scene, loads the data, quits.
# Exit code 0 means the game boots. Used by CI.
set -euo pipefail
cd "$(dirname "$0")/.."
GODOT="${GODOT:-godot}"
dotnet build game/Shaolin36.csproj -nologo
"$GODOT" --headless --path game --import
out="$("$GODOT" --headless --path game -- --smoke 2>&1)" || { echo "$out"; exit 1; }
echo "$out"
grep -q "SMOKE OK" <<<"$out"
