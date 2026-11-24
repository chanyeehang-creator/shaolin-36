#!/usr/bin/env bash
# One-click Play: builds the C# code and runs the game.
# Set GODOT to your Godot 4.7 .NET executable if it is not on PATH.
set -euo pipefail
cd "$(dirname "$0")/.."
dotnet build game/Shaolin36.csproj -v quiet -nologo
"${GODOT:-godot}" --path game "$@"
