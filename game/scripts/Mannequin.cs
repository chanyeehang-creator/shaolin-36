using Godot;
using Shaolin36.Core.Fighters;
using Shaolin36.Core.Poses;
using PoseJoint = Shaolin36.Core.Poses.Joint;

namespace Shaolin36.Game;

/// <summary>
/// The stick-figure fighter: a jointed mannequin built from capsules, posed every tick from the pose sheet
/// (game/data/poses.csv) through <see cref="PoseSheet.PoseAt"/>. Its origin is between the feet and it faces -Z.
/// Look only: the hitboxes are the core's and don't move with the arms. The joints are named like a humanoid rig's
/// bones, so a rigged 3D model can later take the same poses.
/// </summary>
public partial class Mannequin : Node3D
{
    // Body sizes in metres (about 1.78 m tall).
    private const float HipHeight = 0.96f;
    private const float ThighLength = 0.44f, ShinLength = 0.44f, FootDrop = 0.05f;
    private const float UpperArmLength = 0.29f, ForearmLength = 0.27f;
    private const float ShoulderWidth = 0.20f, ShoulderHeight = 0.44f, HipWidth = 0.10f;

    private readonly Node3D _hips = NewJoint("hips"), _waist = NewJoint("waist"), _chest = NewJoint("chest"), _head = NewJoint("head");
    private readonly Node3D _armR = NewJoint("arm_r"), _elbowR = NewJoint("elbow_r"), _armL = NewJoint("arm_l"), _elbowL = NewJoint("elbow_l");
    private readonly Node3D _legR = NewJoint("leg_r"), _kneeR = NewJoint("knee_r"), _legL = NewJoint("leg_l"), _kneeL = NewJoint("knee_l");
    private readonly StandardMaterial3D _skin = new() { Roughness = 0.7f };
    private readonly StandardMaterial3D _dark = new() { AlbedoColor = new Color(0.12f, 0.12f, 0.14f) };

    private Pose? _lastPose, _entry;
    private object? _lastMove;
    private int _lastFrame = -1;

    public Mannequin()
    {
        AddChild(_hips);
        _hips.Position = new Vector3(0, HipHeight, 0);
        // The hips' lean and tilt bend the body at the waist; the legs only turn with the hips, so leg angles are from straight down.
        _hips.AddChild(_waist);
        Part(_waist, new Vector3(0, 0, 0), 0.13f, 0.30f, horizontal: true);

        _waist.AddChild(_chest);
        _chest.Position = new Vector3(0, 0.08f, 0);
        Part(_chest, new Vector3(0, 0.24f, 0), 0.15f, 0.50f);
        // A dark sash across the belly, so the turn of the upper body shows.
        _chest.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.31f, 0.06f, 0.31f) },
            Position = new Vector3(0, 0.07f, 0),
            MaterialOverride = _dark,
        });

        _chest.AddChild(_head);
        _head.Position = new Vector3(0, 0.50f, 0);
        _head.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.11f, Height = 0.24f }, Position = new Vector3(0, 0.13f, 0), MaterialOverride = _skin });
        // A "nose" on the front of the face, so you can see which way each fighter looks.
        _head.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.07f, 0.05f, 0.08f) },
            Position = new Vector3(0, 0.12f, -0.11f),
            MaterialOverride = _dark,
        });

        Arm(_armR, _elbowR, +1);
        Arm(_armL, _elbowL, -1);
        Leg(_legR, _kneeR, +1);
        Leg(_legL, _kneeL, -1);
    }

    private static Node3D NewJoint(string name) => new() { Name = name };

    private void Arm(Node3D shoulder, Node3D elbow, int side)
    {
        _chest.AddChild(shoulder);
        shoulder.Position = new Vector3(side * ShoulderWidth, ShoulderHeight, 0);
        Part(shoulder, new Vector3(0, -UpperArmLength / 2, 0), 0.055f, UpperArmLength + 0.08f);
        shoulder.AddChild(elbow);
        elbow.Position = new Vector3(0, -UpperArmLength, 0);
        Part(elbow, new Vector3(0, -ForearmLength / 2, 0), 0.045f, ForearmLength + 0.06f);
        elbow.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f }, Position = new Vector3(0, -ForearmLength - 0.02f, 0), MaterialOverride = _skin });
    }

    private void Leg(Node3D hip, Node3D knee, int side)
    {
        _hips.AddChild(hip);
        hip.Position = new Vector3(side * HipWidth, -0.03f, 0);
        Part(hip, new Vector3(0, -ThighLength / 2, 0), 0.075f, ThighLength + 0.1f);
        hip.AddChild(knee);
        knee.Position = new Vector3(0, -ThighLength, 0);
        Part(knee, new Vector3(0, -ShinLength / 2, 0), 0.06f, ShinLength + 0.06f);
        knee.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.10f, 0.06f, 0.24f) },
            Position = new Vector3(0, -ShinLength - FootDrop + 0.03f, -0.06f),
            MaterialOverride = _dark,
        });
    }

    private void Part(Node3D parent, Vector3 at, float radius, float length, bool horizontal = false)
    {
        parent.AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = radius, Height = Mathf.Max(length, radius * 2) },
            Position = at,
            RotationDegrees = horizontal ? new Vector3(0, 0, 90) : Vector3.Zero,
            MaterialOverride = _skin,
        });
    }

    /// <summary>The body's colour, and a glow on top of it (0 = none) for the wind-up cue.</summary>
    public void Paint(Color albedo, Color glow, float glowEnergy)
    {
        _skin.AlbedoColor = albedo;
        _skin.EmissionEnabled = glowEnergy > 0;
        _skin.Emission = glow;
        _skin.EmissionEnergyMultiplier = glowEnergy;
    }

    /// <summary>Poses the mannequin for this tick: what <paramref name="f"/> is doing, from the pose sheet.</summary>
    public void Show(Fighter f, PoseSheet sheet)
    {
        // A new move winds up from whatever was showing when it started (a string's next hit from the last hit).
        bool inMove = f.State.IsInMove();
        if (inMove && (!ReferenceEquals(f.Technique, _lastMove) || f.MoveFrame < _lastFrame))
            _entry = _lastPose ?? sheet.Stance;
        _lastMove = inMove ? f.Technique : null;
        _lastFrame = inMove ? f.MoveFrame : -1;
        var pose = sheet.PoseAt(f, _entry);
        _lastPose = pose;
        Apply(pose, plantFeet: !f.IsAirborne && !f.IsDown && !f.State.IsGroundState());
    }

    /// <summary>
    /// Sets every joint to <paramref name="p"/> (a pose gallery or tool can call this directly). With
    /// <paramref name="plantFeet"/>, the lower foot rests on the floor and the other knee straightens until that foot
    /// reaches it too, so stances stand on both feet; a leg raised to about hip level (<see cref="FootLift"/>: a kick, a
    /// knee raise) keeps its foot in the air instead. Leave it off in the air or lying down.
    /// </summary>
    public void Apply(Pose p, bool plantFeet = true)
    {
        _hips.Rotation = new Vector3(0, Mathf.DegToRad(p.Yaw(PoseJoint.Hips)), 0);
        _waist.Rotation = new Vector3(Mathf.DegToRad(-p.Pitch(PoseJoint.Hips)), 0, Mathf.DegToRad(-p.Roll(PoseJoint.Hips)));
        Upright(_chest, p, PoseJoint.Chest);
        Upright(_head, p, PoseJoint.Head);
        Limb(_armR, p, PoseJoint.ArmR, +1);
        Limb(_armL, p, PoseJoint.ArmL, -1);
        Limb(_legR, p, PoseJoint.LegR, +1);
        Limb(_legL, p, PoseJoint.LegL, -1);
        Bend(_elbowR, p.Pitch(PoseJoint.ElbowR));
        Bend(_elbowL, p.Pitch(PoseJoint.ElbowL));
        int kneeR = p.Pitch(PoseJoint.KneeR), kneeL = p.Pitch(PoseJoint.KneeL);
        Knee(_kneeR, kneeR);
        Knee(_kneeL, kneeL);

        _hips.Position = new Vector3(0, HipHeight, 0);
        float right = FootY(_legR, _kneeR), left = FootY(_legL, _kneeL);
        // A kick or a knee raise keeps its foot up (fully from FootLift.FullDegrees); every other pose stands on both.
        int lift = plantFeet ? FootLift.Of(p, out _) : 1000;
        if (lift < 1000 && Mathf.Abs(right - left) > 0.005f)
        {
            // Both feet on the floor: change the higher leg's knee (as little as it can) until its foot is level with
            // the other; if it can't reach, bend the lower leg's knee to meet it.
            bool rightHigher = right > left;
            var (hiHip, hiKnee, hiBend) = rightHigher ? (_legR, _kneeR, kneeR) : (_legL, _kneeL, kneeL);
            var (loHip, loKnee, loBend) = rightHigher ? (_legL, _kneeL, kneeL) : (_legR, _kneeR, kneeR);
            float high = Fit(hiHip, hiKnee, hiBend, Mathf.Min(right, left), 0, out int hiFit);
            Fit(loHip, loKnee, loBend, high, loBend, out int loFit);
            // A leg on its way up (a blend into a kick) lets its foot leave the floor gradually.
            if (lift > 0)
            {
                Knee(hiKnee, Mathf.Lerp(hiFit, hiBend, lift / 1000f));
                Knee(loKnee, Mathf.Lerp(loFit, loBend, lift / 1000f));
            }
        }
        // The lower foot on the floor: bent knees crouch the body instead of floating the feet.
        float lowest = Mathf.Min(FootY(_legR, _kneeR), FootY(_legL, _kneeL));
        _hips.Position = new Vector3(0, HipHeight - lowest, 0);
    }

    private const int MaxKneeBend = 150;

    /// <summary>
    /// Sets the knee to the bend (from <paramref name="least"/> to <see cref="MaxKneeBend"/> degrees) that brings the foot
    /// closest to height <paramref name="target"/>, the nearest to <paramref name="bend"/> among equally good ones.
    /// Returns the foot's height, and the bend in <paramref name="chosen"/>.
    /// </summary>
    private float Fit(Node3D hip, Node3D knee, int bend, float target, int least, out int chosen)
    {
        int best = bend;
        float bestGap = float.MaxValue;
        for (int k = least; k <= MaxKneeBend; k++)
        {
            Knee(knee, k);
            float gap = Mathf.Abs(FootY(hip, knee) - target);
            // Within half a centimetre counts as level: then the smallest change from the pose wins.
            float score = Mathf.Max(gap, 0.005f);
            if (score < bestGap - 0.0001f || (Mathf.Abs(score - bestGap) <= 0.0001f && System.Math.Abs(k - bend) < System.Math.Abs(best - bend)))
            {
                best = k;
                bestGap = score;
            }
        }
        Knee(knee, best);
        chosen = best;
        return FootY(hip, knee);
    }

    // Knees bend backward: a minus turn about X.
    private static void Knee(Node3D n, float degrees) => n.Rotation = new Vector3(Mathf.DegToRad(-degrees), 0, 0);

    /// <summary>Height of a foot's sole above the mannequin's origin with the hips at their standing height.</summary>
    private float FootY(Node3D hip, Node3D knee)
    {
        var sole = _hips.Transform * hip.Transform * knee.Transform * new Vector3(0, -ShinLength - FootDrop, 0);
        return sole.Y;
    }

    // Hips, chest and head point up: forward bend is a minus turn about X, a right tilt a minus turn about Z.
    private static void Upright(Node3D n, Pose p, PoseJoint j) =>
        n.Rotation = new Vector3(Mathf.DegToRad(-p.Pitch(j)), Mathf.DegToRad(p.Yaw(j)), Mathf.DegToRad(-p.Roll(j)));

    // Arms and legs hang down: forward is a plus turn about X; out to the side is +Z on the right, -Z on the left.
    private static void Limb(Node3D n, Pose p, PoseJoint j, int side) =>
        n.Rotation = new Vector3(Mathf.DegToRad(p.Pitch(j)), Mathf.DegToRad(side * p.Yaw(j)), Mathf.DegToRad(side * p.Roll(j)));

    private static void Bend(Node3D n, int degrees) => n.Rotation = new Vector3(Mathf.DegToRad(degrees), 0, 0);
}
