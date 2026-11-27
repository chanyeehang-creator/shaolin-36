---
name: game-animation-principles
description: Disney's 12 animation principles applied to games - anticipation, timing, slow in and out, follow-through, exaggeration, arcs, readable silhouettes, hit-stop and frame data. Use when making or reviewing character poses, wind-ups, strikes, recoveries, hit reactions or enemy tells.
---

<!-- Source: dylantarre/animation-principles skills/01-by-domain/game-development (MIT, https://github.com/dylantarre/animation-principles). Renamed from game-development 27 Nov 2026 and given a Shaolin 36 section at the end; the rest is unchanged. -->

# Game Development Animation

Apply Disney's 12 animation principles to game engines, player feedback, and interactive entertainment.

## Quick Reference

| Principle | Game Implementation |
|-----------|---------------------|
| Squash & Stretch | Character deformation, impact frames |
| Anticipation | Wind-up animations, charge indicators |
| Staging | Camera focus, environmental cues |
| Straight Ahead / Pose to Pose | Procedural vs keyframed animation |
| Follow Through / Overlapping | Capes, hair, weapon trails |
| Slow In / Slow Out | Animation curves, attack/recovery |
| Arc | Projectile paths, jump trajectories |
| Secondary Action | Particles, screen shake, audio sync |
| Timing | Frame data, hit-stop, response windows |
| Exaggeration | Stylized movement, hit reactions |
| Solid Drawing | Consistent silhouettes, read at distance |
| Appeal | Character personality, satisfying feedback |

## Principle Applications

**Squash & Stretch**: Deform characters on landing impact. Stretch during fast movement. Impact frames freeze and squash for power. Keep volume consistent in deformation.

**Anticipation**: Attack wind-ups telegraph to players. Jump squats before leaving ground. Charge attacks show buildup. Enemy tells warn of incoming damage.

**Staging**: Camera frames important action. Environmental lighting guides attention. Enemy placement creates readable combat scenarios. UI doesn't obscure critical gameplay.

**Straight Ahead vs Pose to Pose**: Procedural animation (ragdoll, physics) is straight ahead. Keyframed attack combos are pose to pose. Blend both—keyframed base with procedural secondary motion.

**Follow Through & Overlapping**: Secondary elements (hair, cloth, tails) continue after body stops. Weapon trails persist after swing. Landing recovery extends past initial impact.

**Slow In / Slow Out**: Use animation curves—never linear for character motion. Attack startup fast-out, recovery slow-in. Ease jumps at apex for floatiness control.

**Arc**: Jumping follows parabolic arc. Sword swings trace curved paths. Projectiles arc naturally unless hitscan. Dodge rolls curve rather than linear translate.

**Secondary Action**: Screen shake on impact. Particle bursts on hits. Controller rumble synced to action. Sound design reinforces visual timing.

**Timing**: Hit-stop (freeze frames) emphasizes impact—2-5 frames typical. Attack startup/active/recovery frame data matters for game feel. Response to input under 100ms.

**Exaggeration**: Game animation reads at distance and speed. Exaggerate poses 20-30% beyond realistic. Hit reactions more dramatic than physics would suggest. Stylization serves clarity.

**Solid Drawing**: Silhouettes must read at all zoom levels. Consistent character proportions across animations. Strong poses at keyframes. Avoid tangent lines that confuse form.

**Appeal**: Characters have personality in idle animations. Movement feels satisfying independent of mechanics. Players should enjoy watching their character move.

## Engine Patterns

### Unity
```csharp
// Squash and stretch on landing
IEnumerator LandingSquash() {
    transform.localScale = new Vector3(1.2f, 0.8f, 1.2f);
    yield return new WaitForSeconds(0.05f);
    // Ease back to normal
    float t = 0;
    while (t < 1) {
        t += Time.deltaTime * 8f;
        transform.localScale = Vector3.Lerp(
            new Vector3(1.2f, 0.8f, 1.2f),
            Vector3.one,
            EaseOutElastic(t));
        yield return null;
    }
}

// Hit-stop for impact
IEnumerator HitStop(int frames) {
    Time.timeScale = 0f;
    for (int i = 0; i < frames; i++)
        yield return null;
    Time.timeScale = 1f;
}
```

### Unreal
```cpp
// Animation curve for easing
UPROPERTY(EditAnywhere)
UCurveFloat* JumpArcCurve;

// Apply curve to movement
float CurveValue = JumpArcCurve->GetFloatValue(NormalizedTime);
```

## Timing Reference

| Action Type | Startup | Active | Recovery |
|-------------|---------|--------|----------|
| Light attack | 3-6f | 2-4f | 8-12f |
| Heavy attack | 12-20f | 4-8f | 16-24f |
| Jump | 3-4f | -- | 4-6f |
| Dodge | 2-4f | 8-12f | 6-10f |

Frame data at 60fps. Adjust for target framerate.

## Shaolin 36

- **Poses are data.**
  - Each move's keys are in `game/data/poses.csv`: `<id>.windup@p`, `.strike`, `.strike@p` and `.recovery@p`, where p is a percentage of that phase.
  - `MoveTimeline` (core) moves straight between moving keys and eases in and out at rests. A key that repeats the one before it is a hold.
  - The joint conventions and key rules are in the comments at the top of poses.csv.
- **Chan's rhythm for kung fu:**
  - a slow, readable wind-up (anticipation);
  - then an instant strike that snaps on the first active frame;
  - no frozen frames except on hit confirms, unless Chan asks for a pause (铁砂掌 pauses briefly at the top of its wind-up);
  - the finish can be held, then eased back.
- **Match the reference.**
  - Chan supplies screenshots of real Shaolin forms for each move.
  - Render the keys with `scripts\pose-shots.bat <move>`, which writes `pose-shots/<move>.strip.png`.
  - Compare them side by side with the reference before asking Chan to play.
- **Exaggerate for readability,** enemy tells most of all. Use big silhouettes that read from the opponent's side.
- **The frame data in `game/data/techniques.csv` wins over the timing table above.** Hit-stop is the core's `FeelClock`, not `Time.timeScale`. The Unity and Unreal snippets show patterns only.
- **Later:** a rigged 3D humanoid will use the same joint names.
