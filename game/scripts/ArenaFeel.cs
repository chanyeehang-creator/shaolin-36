using System.Collections.Generic;
using Godot;
using Shaolin36.Core.Combat;
using Shaolin36.Core.Feel;
using Side = Shaolin36.Core.Combat.Side;

namespace Shaolin36.Game;

/// <summary>
/// The feel kit's show side (Stage 2 step 1): sparks, sound and camera shake and push for each strike.
/// The core decides which row of feel.csv a strike uses and freezes the fight for its hit-stop; this
/// only draws and plays it. Everything here runs on screen time, so the sparks keep flying and the
/// camera keeps shaking while the fighters are frozen.
/// </summary>
public partial class Arena
{
    // Third-person lock-on camera: behind you and a little to the right, looking past you at the dummy.
    // Chan's playtest: 30% further out than the first version (3.4, 2.7, 1.7).
    private const float CameraBack = 4.4f;    // metres behind you
    private const float CameraHeight = 3.5f;  // metres above the floor
    private const float CameraSide = 2.2f;    // metres to your right, so you don't block the view
    private const float CameraLookHeight = 0.9f;
    private const float CameraFollowPerSecond = 7f; // how quickly it swings round after you (exponential)
    private Vector3 _cameraBase, _cameraLook;
    private bool _cameraPlaced;
    private const float StrikeHeight = 1.3f; // metres: where on the capsule sparks fly
    private const float BodyRadius = 0.4f;
    private const float ShakeTailSeconds = 0.15f; // shake keeps dying away this long after the freeze
    private const float PushInSeconds = 0.04f;
    private const float PushOutSeconds = 0.3f;
    private const float SparkSeconds = 0.3f;
    private const float FlashSeconds = 0.12f;
    private const int Voices = 6;
    private const int MixRate = 22050;

    private Camera3D _camera = null!;
    private float _shakeCm, _shakeAge, _shakeLength;
    private float _pushCm, _pushAge = 99f, _pushHold;
    private Vector3 _pushDir;

    private sealed class Spark
    {
        public Node3D Node = null!;
        public OmniLight3D Light = null!;
        public float Energy;
        public float Age;
    }

    private readonly List<Spark> _sparks = new();

    // ---- hit reactions: how far each capsule leans back (degrees), decaying back to upright.
    private readonly float[] _lean = new float[2];
    private const float LeanRecoverPerSecond = 7f; // exponential: about half gone in 0.1 s
    private const float KoLean = 80f;              // the loser tips over and stays down
    private const float DownLean = 90f;            // knocked down: flat on the floor

    /// <summary>Lean kicks for each kind of strike: (defender, attacker) in degrees.</summary>
    private static (float defender, float attacker) LeanKick(FeelEvent e) => e switch
    {
        FeelEvent.Hit => (10f, 0f),
        FeelEvent.HeavyHit => (18f, 0f),
        FeelEvent.EmpoweredHit => (26f, 0f),
        FeelEvent.ArmorHit => (3f, 0f),
        FeelEvent.Block => (5f, 0f),
        FeelEvent.PerfectParry => (0f, 14f),
        FeelEvent.ClashRecoil => (10f, 10f),
        FeelEvent.ClashBreak => (18f, 0f),
        FeelEvent.ClashLost => (0f, 14f),
        FeelEvent.PerfectClash => (20f, 4f),
        FeelEvent.Ko => (35f, 0f),
        FeelEvent.Grab => (6f, 0f),
        FeelEvent.Throw => (30f, 0f),
        _ => (0f, 0f),
    };

    private void Kick(Side side, float degrees)
    {
        if (degrees > _lean[(int)side]) _lean[(int)side] = degrees;
    }

    /// <summary>How far the fighter on <paramref name="side"/> leans back now, with a wobble while staggered.</summary>
    private float LeanDegrees(Side side)
    {
        var f = _duel[side];
        if (f.IsDown) return KoLean;
        // Placeholder knockdown animations: flat on the floor, then the quick rise tips back up to standing.
        switch (f.State)
        {
            case Shaolin36.Core.Fighters.FighterState.KnockedDown:
            case Shaolin36.Core.Fighters.FighterState.Rolling:
                return DownLean;
            case Shaolin36.Core.Fighters.FighterState.Rising:
                return DownLean * (1f - (f.GetUpFrame + 1) / (float)f.GetUpTotalFrames);
        }
        float wobble = f.State == Shaolin36.Core.Fighters.FighterState.Stagger && _feelOn
            ? 4f * Mathf.Sin(Time.GetTicksMsec() / 1000f * 14f)
            : 0f;
        return _lean[(int)side] + wobble;
    }
    private readonly Dictionary<FeelSound, AudioStreamWav> _sounds = new();
    private readonly List<AudioStreamPlayer> _voices = new();
    private int _nextVoice;

    /// <summary>Spark colour for each kind of strike.</summary>
    private static Color SparkColor(FeelEvent e) => e switch
    {
        FeelEvent.Hit => new Color(1.00f, 0.85f, 0.55f),
        FeelEvent.HeavyHit => new Color(1.00f, 0.55f, 0.20f),
        FeelEvent.EmpoweredHit => new Color(1.00f, 0.80f, 0.15f),
        FeelEvent.ArmorHit => new Color(0.90f, 0.65f, 0.20f),
        FeelEvent.Block => new Color(0.45f, 0.80f, 1.00f),
        FeelEvent.PerfectParry => new Color(0.55f, 1.00f, 0.60f),
        FeelEvent.ClashRecoil or FeelEvent.ClashBreak or FeelEvent.ClashLost => new Color(1.00f, 0.95f, 0.60f),
        FeelEvent.PerfectClash => new Color(1.00f, 1.00f, 0.90f),
        FeelEvent.Ko => new Color(1.00f, 0.30f, 0.20f),
        FeelEvent.Throw => new Color(0.95f, 0.55f, 0.25f),
        _ => new Color(1, 1, 1),
    };

    private void PlayCue(FeelCue cue)
    {
        var row = cue.Row;
        var at = StrikePoint(cue.Hit);
        float freezeSeconds = row.HitStopFrames / 60f;

        if (row.SparkCm > 0) SpawnSpark(at, row.SparkCm / 100f, SparkColor(cue.Event));

        var (defenderLean, attackerLean) = LeanKick(cue.Event);
        Kick(cue.Hit.Defender, defenderLean);
        Kick(cue.Hit.Attacker, attackerLean);

        if (row.ShakeCm > 0 && row.ShakeCm >= _shakeCm * Remaining(_shakeAge, _shakeLength))
        {
            _shakeCm = row.ShakeCm;
            _shakeAge = 0;
            _shakeLength = freezeSeconds + ShakeTailSeconds;
        }

        if (row.PushCm > 0)
        {
            _pushCm = row.PushCm;
            _pushAge = 0;
            _pushHold = freezeSeconds;
            _pushDir = (at - _cameraBase).Normalized();
        }

        if (row.Sound != FeelSound.None && _sounds.TryGetValue(row.Sound, out var stream))
        {
            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Count;
            voice.Stream = stream;
            // A small pitch change each time, so a string doesn't sound like a machine gun.
            voice.PitchScale = 0.94f + 0.04f * (_nextVoice % 4);
            voice.Play();
        }
    }

    // ---------------------------------------------------------------- 仙辽术 sounds (Chan's ask: placeholders to feel the heal)

    private AudioStreamWav _healChannel = null!, _healDone = null!;
    /// <summary>One channel voice per fighter, so a broken heal can cut its own hum short.</summary>
    private readonly AudioStreamPlayer[] _healVoices = new AudioStreamPlayer[2];
    private readonly bool[] _healHumming = new bool[2];

    private void BuildHealSounds()
    {
        _healChannel = Wav(HealChannelSamples());
        _healDone = Wav(HealDoneSamples());
        for (int i = 0; i < 2; i++)
        {
            _healVoices[i] = new AudioStreamPlayer { VolumeDb = -8f };
            AddChild(_healVoices[i]);
        }
    }

    /// <summary>The heal's wind-up starts: a soft hum that swells and rises through the wind-up and channel.</summary>
    private void PlayHealChannel(Side side)
    {
        if (!_feelOn) return;
        var v = _healVoices[(int)side];
        v.Stream = _healChannel;
        v.VolumeDb = -8f;
        v.Play();
        _healHumming[(int)side] = true;
    }

    /// <summary>The heal lands: the hum stops and a bright rising chime plays.</summary>
    private void PlayHealDone(Side side)
    {
        var v = _healVoices[(int)side];
        v.Stop();
        _healHumming[(int)side] = false;
        if (!_feelOn) return;
        v.Stream = _healDone;
        v.VolumeDb = -5f;
        v.Play();
    }

    /// <summary>The heal was broken before it landed: cut the hum (the success chime is left to ring).</summary>
    private void StopHealChannel(Side side)
    {
        if (!_healHumming[(int)side]) return;
        _healHumming[(int)side] = false;
        _healVoices[(int)side].Stop();
    }

    private static AudioStreamWav Wav(float[] samples) => new()
    {
        Format = AudioStreamWav.FormatEnum.Format16Bits,
        MixRate = MixRate,
        Stereo = false,
        Data = Pcm16(samples),
    };

    /// <summary>1.5 s (the wind-up and channel): a low drone with a fifth above, a tone gliding up an octave, and a slow shimmer.</summary>
    private static float[] HealChannelSamples()
    {
        const float seconds = 1.5f;
        var buf = new float[(int)(seconds * MixRate)];
        float phase = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)MixRate;
            float k = t / seconds;
            float swell = Mathf.Min(1f, t / 0.25f) * (0.5f + 0.5f * k);
            float glide = 330f * Mathf.Pow(2f, k); // 330 Hz rising to 660 Hz
            phase += Mathf.Tau * glide / MixRate;
            float shimmer = 0.5f + 0.5f * Mathf.Sin(Mathf.Tau * 6f * t);
            buf[i] = swell * (0.5f * Sine(t, 165f) + 0.3f * Sine(t, 247.5f) + 0.35f * Mathf.Sin(phase) * shimmer);
        }
        return Normalise(buf, 0.55f);
    }

    /// <summary>0.9 s: a quick C-E-G-C chime, each note ringing on, like a small bell tree.</summary>
    private static float[] HealDoneSamples()
    {
        const float seconds = 0.9f;
        float[] notes = { 523.3f, 659.3f, 784f, 1046.5f };
        var buf = new float[(int)(seconds * MixRate)];
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)MixRate;
            float v = 0;
            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * 0.07f;
                if (t < start) continue;
                float u = t - start;
                v += (Sine(u, notes[n]) + 0.3f * Sine(u, notes[n] * 2.01f)) * Mathf.Exp(-u * 4f) * Mathf.Min(1f, u * 400f);
            }
            buf[i] = v;
        }
        return Normalise(buf, 0.7f);
    }

    /// <summary>Scale to <paramref name="level"/> peak, with a 3 ms fade out so nothing clicks.</summary>
    private static float[] Normalise(float[] buf, float level)
    {
        float peak = 0.0001f;
        foreach (var x in buf) peak = Mathf.Max(peak, Mathf.Abs(x));
        int fade = MixRate * 3 / 1000;
        for (int i = 0; i < buf.Length; i++)
            buf[i] = buf[i] / peak * level * Mathf.Min(1f, (buf.Length - 1 - i) / (float)fade);
        return buf;
    }

    private static float Remaining(float age, float length) => length <= 0 ? 0 : Mathf.Max(0, 1 - age / length);

    /// <summary>Where the strike meets: the defender's near side for a hit or a parry, between the two for a clash.</summary>
    private Vector3 StrikePoint(HitEvent hit)
    {
        var a = Floor(_duel[hit.Attacker].Position);
        var d = Floor(_duel[hit.Defender].Position);
        var at = hit.Clash is ClashOutcome.BothRecoil or ClashOutcome.TargetWins or ClashOutcome.PerfectClash or ClashOutcome.StrikerWins
            ? (a + d) / 2
            : d + (a - d).Normalized() * BodyRadius;
        return at + new Vector3(0, StrikeHeight, 0);
    }

    /// <summary>
    /// Where the camera sits and looks before shake and push: behind the player on the line from the dummy,
    /// eased so it swings round smoothly as you circle.
    /// </summary>
    private void FollowCamera(float delta)
    {
        var me = Floor(_duel.One.Position);
        var them = Floor(_duel.Two.Position);
        var toward = them - me;
        if (toward.LengthSquared() < 0.0001f) toward = -_player.GlobalTransform.Basis.Z;
        toward.Y = 0;
        var forward = toward.Normalized();
        var right = new Vector3(-forward.Z, 0, forward.X);
        var spot = me - forward * CameraBack + right * CameraSide + Vector3.Up * CameraHeight;
        // Look at the dummy itself: you stand to the left of the picture, the dummy is in the middle.
        var look = them + Vector3.Up * CameraLookHeight;

        float k = _cameraPlaced ? 1 - Mathf.Exp(-CameraFollowPerSecond * delta) : 1;
        _cameraBase = _cameraBase.Lerp(spot, k);
        _cameraLook = _cameraLook.Lerp(look, k);
        _cameraPlaced = true;
    }

    private void SpawnSpark(Vector3 at, float sizeM, Color color)
    {
        var particles = new CpuParticles3D
        {
            Position = at,
            OneShot = true,
            Explosiveness = 1f,
            Amount = 14 + (int)(sizeM * 40),
            Lifetime = SparkSeconds,
            Direction = new Vector3(0, 1, 0),
            Spread = 180f,
            // Most sparks travel about sizeM before they fade.
            InitialVelocityMin = sizeM * 2.5f,
            InitialVelocityMax = sizeM * 7f,
            DampingMin = sizeM * 6f,
            DampingMax = sizeM * 10f,
            Gravity = new Vector3(0, -6f, 0),
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.4f,
            Color = color,
            Mesh = new SphereMesh
            {
                Radius = 0.03f,
                Height = 0.06f,
                RadialSegments = 6,
                Rings = 3,
                Material = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    VertexColorUseAsAlbedo = true,
                },
            },
        };
        AddChild(particles);
        particles.Emitting = true;

        float energy = 1.5f + sizeM * 4f;
        var light = new OmniLight3D { Position = at, LightColor = color, LightEnergy = energy, OmniRange = 1.5f + sizeM * 2f };
        AddChild(light);
        _sparks.Add(new Spark { Node = particles, Light = light, Energy = energy });
    }

    /// <summary>Screen-time part of the kit: camera shake and push, spark flashes fading out.</summary>
    private void UpdateFeel(float delta)
    {
        if (_camera == null) return;
        FollowCamera(delta);
        _camera.Position = _cameraBase;
        _camera.LookAt(_cameraLook, Vector3.Up);

        var offset = Vector3.Zero;
        if (_shakeLength > 0 && _shakeAge < _shakeLength)
        {
            _shakeAge += delta;
            float left = Remaining(_shakeAge, _shakeLength);
            float amp = _shakeCm / 100f * left * left;
            float t = _shakeAge;
            var b = _camera.Basis;
            offset += (b.X * (Mathf.Sin(t * 83f) + 0.5f * Mathf.Sin(t * 147f)) + b.Y * Mathf.Sin(t * 61f + 1.3f)) * amp;
        }

        _pushAge += delta;
        float push = _pushAge < PushInSeconds ? _pushAge / PushInSeconds
            : _pushAge < PushInSeconds + _pushHold ? 1f
            : Mathf.Max(0, 1 - (_pushAge - PushInSeconds - _pushHold) / PushOutSeconds);
        push = push * push * (3 - 2 * push); // ease in and out
        offset += _pushDir * (_pushCm / 100f * push);

        _camera.Position = _cameraBase + offset;

        // Leans hold while the fight is frozen in hit-stop, then spring back upright.
        if (!_clock.IsFrozen)
        {
            float keep = Mathf.Exp(-LeanRecoverPerSecond * delta);
            _lean[0] *= keep;
            _lean[1] *= keep;
        }

        for (int i = _sparks.Count - 1; i >= 0; i--)
        {
            var s = _sparks[i];
            s.Age += delta;
            s.Light.LightEnergy = s.Energy * Mathf.Max(0, 1 - s.Age / FlashSeconds);
            if (s.Age < SparkSeconds + 0.2f) continue;
            s.Node.QueueFree();
            s.Light.QueueFree();
            _sparks.RemoveAt(i);
        }
    }

    // ---------------------------------------------------------------- sounds

    /// <summary>
    /// Placeholder sounds, made in code so the kit works before there are real recordings: a few
    /// sine partials and filtered noise per sound. Swap in real files later; feel.csv stays the same.
    /// </summary>
    private void BuildSounds()
    {
        foreach (FeelSound s in System.Enum.GetValues(typeof(FeelSound)))
        {
            if (s == FeelSound.None) continue;
            _sounds[s] = new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format16Bits,
                MixRate = MixRate,
                Stereo = false,
                Data = Pcm16(Synth(s)),
            };
        }
        for (int i = 0; i < Voices; i++)
        {
            var p = new AudioStreamPlayer { VolumeDb = -4f };
            AddChild(p);
            _voices.Add(p);
        }
    }

    private static float[] Synth(FeelSound sound)
    {
        float seconds = sound switch
        {
            FeelSound.Thud => 0.25f,
            FeelSound.Smack => 0.14f,
            FeelSound.Crack => 0.45f,
            FeelSound.Block => 0.1f,
            FeelSound.Ring => 0.75f,
            FeelSound.Clang => 0.55f,
            FeelSound.Gong => 1.6f,
            FeelSound.Whoosh => 0.35f,
            _ => 0.1f,
        };
        var buf = new float[(int)(seconds * MixRate)];
        uint seed = 0x9E3779B9u ^ (uint)sound;
        float low = 0, phase = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            float t = i / (float)MixRate;
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            float noise = (seed / (float)uint.MaxValue) * 2f - 1f;
            float v;
            switch (sound)
            {
                case FeelSound.Thud:
                {
                    float f = 45f + 85f * Mathf.Exp(-t * 25f); // 130 Hz falling to 45 Hz
                    phase += Mathf.Tau * f / MixRate;
                    low += 0.15f * (noise - low);
                    v = Mathf.Sin(phase) * Mathf.Exp(-t * 16f) + low * 2.5f * Mathf.Exp(-t * 60f);
                    break;
                }
                case FeelSound.Smack:
                    low += 0.45f * (noise - low);
                    v = low * Mathf.Exp(-t * 40f) + 0.5f * Sine(t, 210f) * Mathf.Exp(-t * 35f);
                    break;
                case FeelSound.Crack:
                {
                    float f = 40f + 70f * Mathf.Exp(-t * 15f);
                    phase += Mathf.Tau * f / MixRate;
                    v = noise * Mathf.Exp(-t * 30f) + 1.2f * Mathf.Sin(phase) * Mathf.Exp(-t * 7f)
                        + 0.25f * Sine(t, 1400f) * Mathf.Exp(-t * 25f);
                    break;
                }
                case FeelSound.Block:
                    low += 0.2f * (noise - low);
                    v = low * 2f * Mathf.Exp(-t * 70f) + 0.6f * Sine(t, 330f) * Mathf.Exp(-t * 45f);
                    break;
                case FeelSound.Ring:
                    v = Sine(t, 1320f) * Mathf.Exp(-t * 5f) + 0.6f * Sine(t, 1998f) * Mathf.Exp(-t * 7f)
                        + 0.4f * Sine(t, 2830f) * Mathf.Exp(-t * 10f) + noise * Mathf.Exp(-t * 250f);
                    break;
                case FeelSound.Clang:
                    v = 1.2f * noise * Mathf.Exp(-t * 45f) + Sine(t, 523f) * Mathf.Exp(-t * 6f)
                        + 0.7f * Sine(t, 807f) * Mathf.Exp(-t * 7f) + 0.5f * Sine(t, 1187f) * Mathf.Exp(-t * 9f)
                        + 0.35f * Sine(t, 1663f) * Mathf.Exp(-t * 12f);
                    break;
                case FeelSound.Gong:
                    low += 0.1f * (noise - low);
                    v = (Sine(t, 98f) + 0.7f * Sine(t, 147.5f) + 0.5f * Sine(t, 196.8f) + 0.3f * Sine(t, 262f))
                        * Mathf.Exp(-t * 2.2f) * Mathf.Min(1f, t * 200f) + low * 3f * Mathf.Exp(-t * 30f);
                    break;
                case FeelSound.Whoosh:
                {
                    // Band of noise swelling in and out, its pitch sweeping down: a light rush of air.
                    float cut = 0.05f + 0.25f * Mathf.Exp(-t * 6f);
                    low += cut * (noise - low);
                    float swell = Mathf.Sin(Mathf.Pi * Mathf.Min(1f, t / 0.35f));
                    v = low * swell;
                    break;
                }
                default:
                    v = 0;
                    break;
            }
            buf[i] = v;
        }

        // Normalise to the same peak, with a 3 ms fade out so nothing clicks.
        float peak = 0.0001f;
        foreach (var x in buf) peak = Mathf.Max(peak, Mathf.Abs(x));
        int fade = MixRate * 3 / 1000;
        for (int i = 0; i < buf.Length; i++)
        {
            float tail = Mathf.Min(1f, (buf.Length - 1 - i) / (float)fade);
            // The whoosh is a light sound: quieter than the strikes.
            buf[i] = buf[i] / peak * (sound == FeelSound.Whoosh ? 0.45f : 0.8f) * tail;
        }
        return buf;
    }

    private static float Sine(float t, float hz) => Mathf.Sin(Mathf.Tau * hz * t);

    private static byte[] Pcm16(float[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short v = (short)Mathf.Clamp(samples[i] * 32767f, -32768f, 32767f);
            bytes[2 * i] = (byte)(v & 0xff);
            bytes[2 * i + 1] = (byte)((v >> 8) & 0xff);
        }
        return bytes;
    }
}
