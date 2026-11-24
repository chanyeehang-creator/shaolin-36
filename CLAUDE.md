# Working on Shaolin 36

- **One change, then test.** Each brief is one change. Add or update tests for it, run `scripts/test.sh`, and keep CI green before moving on.
- **Rules stay engine-free.** `src/Shaolin36.Core` must not reference Godot or Unity. It targets netstandard2.1 with C# 9 so it stays Unity-compatible: no records, no `init`, no C# 10+ syntax there.
- **Determinism.** Rules run on a fixed 60 Hz tick (`Sim.TicksPerSecond`). No wall-clock time, no unseeded randomness, no floating-point state that depends on frame rate inside the core.
- **Data lives in sheets.** Tunable numbers go in `game/data/*.csv`, checked by `tools/DataCheck`, never hard-coded in the core.
- **Godot side** (`game/`) only shows and drives the core. Headless boot check: `scripts/smoke.sh` (needs `GODOT`).
