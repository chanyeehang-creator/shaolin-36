---
name: game-design-theory
description: Game design theory - MDA and DDE, flow, player motivation (self-determination, Bartle, Quantic Foundry), reward types and schedules, balance principles, ludonarrative consonance. Use when judging why a system is or isn't engaging, or grounding a design decision in theory.
---

<!-- Source: game-design-theory v2.0.0 (claudskills.com copy; pluginagentmarketplace/custom-plugin-game-developer). Trimmed 19 Nov 2026: tool-specific frontmatter. references/game-design-theory.md is AlterLab GameForge docs/game-design-theory.md (MIT, Cem Ipek, AlterLab Creative Technologies Laboratory). GameForge agents and /gameforge: commands named in the reference are not installed: skip them. Copied from the Reliance project to Shaolin 36 on 27 Nov 2026. -->

# Game Design Theory

## The MDA Framework

```
┌─────────────────────────────────────────────────────────────┐
│                    MDA FRAMEWORK                             │
├─────────────────────────────────────────────────────────────┤
│  MECHANICS (Rules):                                          │
│  → Player actions, constraints, state changes               │
│  → Example: Jump has height limit, costs stamina            │
│                              ↓                               │
│  DYNAMICS (Behavior):                                        │
│  → Emergent gameplay from mechanic interactions             │
│  → Example: Wall-jump combos, speedrun routes               │
│                              ↓                               │
│  AESTHETICS (Experience):                                    │
│  → Emotional responses: Fun, tension, achievement           │
│  → Example: Flow state, satisfaction, immersion             │
└─────────────────────────────────────────────────────────────┘
```

## Core Game Loop

```
┌─────────────────────────────────────────────────────────────┐
│                    ENGAGEMENT LOOP                           │
├─────────────────────────────────────────────────────────────┤
│  1. INPUT    → Player takes action                          │
│  2. PROCESS  → Game calculates results                      │
│  3. FEEDBACK → Immediate visual/audio response              │
│  4. REWARD   → Progress, points, unlocks                    │
│  5. REPEAT   → Loop invites next iteration                  │
│                                                              │
│  Loop Quality Criteria:                                      │
│  ✓ Fast feedback (< 100ms)                                  │
│  ✓ Clear causation                                          │
│  ✓ Rewarding outcomes                                       │
│  ✓ Compelling repetition                                    │
└─────────────────────────────────────────────────────────────┘
```

## Flow Channel (Csikszentmihalyi)

```
     Anxiety
         ↑
  Hard   │     ████
         │   ██████   ← FLOW CHANNEL
Skill    │ ████████      (Optimal Engagement)
Level    │████████████
  Easy   │██████████████
         └──────────────────→
           Low    Challenge    High

TARGET: Match challenge to player skill
```

## Player Psychology

### Bartle's Player Types

| Type | Motivation | Design For |
|------|------------|------------|
| Achiever | Goals, progression | Achievements, levels |
| Explorer | Discovery, secrets | Hidden content, lore |
| Socializer | Community | Chat, guilds, co-op |
| Killer | Competition | PvP, leaderboards |

### Motivation Drivers

```
SELF-DETERMINATION THEORY:
┌─────────────────────────────────────────────────────────────┐
│  AUTONOMY:   Choice and control over actions               │
│  COMPETENCE: Mastery and skill demonstration               │
│  RELATEDNESS: Connection to characters/community           │
└─────────────────────────────────────────────────────────────┘
```

## Reward Systems

```
REWARD TYPES:
┌─────────────────────────────────────────────────────────────┐
│  INTRINSIC (Internal):                                       │
│  • Achievement satisfaction                                 │
│  • Creative expression                                      │
│  • Curiosity fulfillment                                    │
│  • Skill mastery                                            │
├─────────────────────────────────────────────────────────────┤
│  EXTRINSIC (External):                                       │
│  • Points, scores                                           │
│  • Unlocks, cosmetics                                       │
│  • Leaderboard position                                     │
│  • Currency rewards                                         │
└─────────────────────────────────────────────────────────────┘

REWARD SCHEDULING:
• Fixed Ratio: Every N actions (predictable)
• Variable Ratio: Random timing (engaging but ethical concerns)
• Fixed Interval: Every N seconds
• Milestone: At progression checkpoints
```

## Balance Principles

| Aspect | Goal | Technique |
|--------|------|-----------|
| Mechanical | All options viable | Counter-play, trade-offs |
| Economic | Meaningful scarcity | Sinks and faucets |
| Difficulty | Appropriate challenge | Dynamic scaling |
| Competitive | Fair play | Mirror balance, no dominance |

## 🔧 Troubleshooting

```
┌─────────────────────────────────────────────────────────────┐
│ PROBLEM: Players find game boring                           │
├─────────────────────────────────────────────────────────────┤
│ ROOT CAUSES:                                                 │
│ • Challenge too easy (below flow channel)                   │
│ • No clear goals or progression                             │
│ • Feedback loop too slow                                    │
├─────────────────────────────────────────────────────────────┤
│ SOLUTIONS:                                                   │
│ → Increase challenge curve                                  │
│ → Add clear milestones and rewards                          │
│ → Speed up core loop, add variety                           │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ PROBLEM: Players frustrated / quitting                      │
├─────────────────────────────────────────────────────────────┤
│ ROOT CAUSES:                                                 │
│ • Difficulty spike (above flow channel)                     │
│ • Unclear mechanics or feedback                             │
│ • Unfair or random feeling deaths                           │
├─────────────────────────────────────────────────────────────┤
│ SOLUTIONS:                                                   │
│ → Smooth difficulty curve                                   │
│ → Improve tutorial and feedback                             │
│ → Make deaths feel fair and educational                     │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ PROBLEM: Dominant strategy / no variety                     │
├─────────────────────────────────────────────────────────────┤
│ SOLUTIONS:                                                   │
│ → Add counter-play to dominant options                      │
│ → Buff underused alternatives                               │
│ → Create situational advantages                             │
└─────────────────────────────────────────────────────────────┘
```

## Design Checklist

```
PRE-PRODUCTION:
□ Target audience defined
□ Core loop documented
□ Unique selling point clear
□ Reference games analyzed

PRODUCTION:
□ Mechanics serve aesthetics
□ Feedback loops verified
□ Balance spreadsheets maintained
□ Playtest schedule in place

POLISH:
□ First-time user experience tested
□ Difficulty curve validated
□ Reward timing optimized
□ Edge cases handled
```

---

**Use this skill**: When designing game systems, understanding player psychology, or balancing gameplay.


## Deeper reference

For MDA and DDE, self-determination theory, flow, player motivation models, Schell's tetrad, Koster, ludonarrative consonance, procedural rhetoric, game feel, accessibility as design, procedural generation, onboarding and design pillars, read `references/game-design-theory.md` (by section; it is long). Its AI-content section mentions GameForge tools that are not installed here.
