using System.Collections.Generic;
using Godot;
using Shaolin36.Core.Fighters;

namespace Shaolin36.Game;

/// <summary>What a button press means outside the fight itself.</summary>
public enum Command
{
    None,
    /// <summary>Open or close the loadout screen (Tab, Esc, or the pad's Start button).</summary>
    Menu,
    /// <summary>Start the duel again (Enter; on a pad, Start after a KO or the loadout screen's restart row).</summary>
    Restart,
    /// <summary>Make the dummy stand still, or attack again (T, or the pad's Back/Select button).</summary>
    DummyToggle,
    MenuUp,
    MenuDown,
    MenuLeft,
    MenuRight,
    /// <summary>Half speed on and off (F1), for looking at timing.</summary>
    SlowMotion,
    /// <summary>Floating debug text (damage numbers, Blocked!, Parried!) on and off (F2).</summary>
    FloatingText,
    /// <summary>The feel kit (hit-stop, sparks, sound, camera) on and off (F3), to compare with and without.</summary>
    FeelKit,
}

/// <summary>
/// Maps every device onto the 8 slots. Keyboard + mouse and any gamepad work at the same
/// time; nothing here knows what technique sits on a slot. Gamepad buttons use Godot's
/// standard layout, which Godot fills in for known pads (the Logitech Dual Action is one):
/// the pad's left face button is □, top △, right ○, bottom ✕.
/// </summary>
public static class Controls
{
    /// <summary>A key or mouse button that presses a slot, with the label shown on screen.</summary>
    private sealed class Binding
    {
        public Binding(Slot slot, string label, Key[] keys, MouseButton[] mouse, JoyButton[] pad, JoyAxis? padAxis)
        {
            Slot = slot;
            Label = label;
            Keys = keys;
            Mouse = mouse;
            Pad = pad;
            PadAxis = padAxis;
        }

        public Slot Slot { get; }
        public string Label { get; }
        public Key[] Keys { get; }
        public MouseButton[] Mouse { get; }
        public JoyButton[] Pad { get; }
        public JoyAxis? PadAxis { get; }
    }

    private static readonly Binding[] Bindings =
    {
        new(Slot.Square,   "J / left click · pad 1 (left)",  new[] { Key.J },          new[] { MouseButton.Left },  new[] { JoyButton.X }, null),
        new(Slot.Triangle, "I · pad 4 (top)",                new[] { Key.I },          new MouseButton[0],          new[] { JoyButton.Y }, null),
        new(Slot.Circle,   "L · pad 3 (right)",              new[] { Key.L },          new MouseButton[0],          new[] { JoyButton.B }, null),
        new(Slot.Cross,    "K / Space · pad 2 (bottom)",     new[] { Key.K, Key.Space }, new MouseButton[0],        new[] { JoyButton.A }, null),
        new(Slot.L1,       "Q / right click · pad 5",        new[] { Key.Q },          new[] { MouseButton.Right }, new[] { JoyButton.LeftShoulder }, null),
        new(Slot.L2,       "Shift · pad 7",                  new[] { Key.Shift },      new MouseButton[0],          new JoyButton[0], JoyAxis.TriggerLeft),
        new(Slot.R1,       "E · pad 6",                      new[] { Key.E },          new MouseButton[0],          new[] { JoyButton.RightShoulder }, null),
        new(Slot.R2,       "F · pad 8",                      new[] { Key.F },          new MouseButton[0],          new JoyButton[0], JoyAxis.TriggerRight),
    };

    /// <summary>How far a trigger or stick must move to count as pressed.</summary>
    private const float Deadzone = 0.5f;

    // Last value of each pad's trigger, to turn a trigger pull into one press.
    private static readonly Dictionary<(int device, JoyAxis axis), float> LastAxis = new();

    /// <summary>The keys and buttons for a slot, for the loadout screen.</summary>
    public static string LabelOf(Slot slot)
    {
        foreach (var b in Bindings) if (b.Slot == slot) return b.Label;
        return "";
    }

    /// <summary>
    /// The slot this event presses, or null. Only the moment a button goes down counts:
    /// key repeats, releases and a trigger held down don't press again.
    /// </summary>
    public static Slot? SlotPressed(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey key when key.Pressed && !key.Echo:
                foreach (var b in Bindings)
                    foreach (var k in b.Keys)
                        if (key.PhysicalKeycode == k) return b.Slot;
                return null;

            case InputEventMouseButton mouse when mouse.Pressed:
                foreach (var b in Bindings)
                    foreach (var m in b.Mouse)
                        if (mouse.ButtonIndex == m) return b.Slot;
                return null;

            case InputEventJoypadButton pad when pad.Pressed:
                foreach (var b in Bindings)
                    foreach (var j in b.Pad)
                        if (pad.ButtonIndex == j) return b.Slot;
                return null;

            case InputEventJoypadMotion motion:
                foreach (var b in Bindings)
                {
                    if (b.PadAxis != motion.Axis) continue;
                    var id = (motion.Device, motion.Axis);
                    LastAxis.TryGetValue(id, out var before);
                    LastAxis[id] = motion.AxisValue;
                    if (before < Deadzone && motion.AxisValue >= Deadzone) return b.Slot;
                }
                return null;

            default:
                return null;
        }
    }

    /// <summary>The non-fight command this event gives, if any.</summary>
    public static Command CommandOf(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey key when key.Pressed:
                switch (key.PhysicalKeycode)
                {
                    case Key.Tab:
                    case Key.Escape: return key.Echo ? Command.None : Command.Menu;
                    case Key.Enter:
                    case Key.KpEnter: return key.Echo ? Command.None : Command.Restart;
                    case Key.F1: return key.Echo ? Command.None : Command.SlowMotion;
                    case Key.F2: return key.Echo ? Command.None : Command.FloatingText;
                    case Key.F3: return key.Echo ? Command.None : Command.FeelKit;
                    case Key.T: return key.Echo ? Command.None : Command.DummyToggle;
                    case Key.W:
                    case Key.Up: return Command.MenuUp;
                    case Key.S:
                    case Key.Down: return Command.MenuDown;
                    case Key.A:
                    case Key.Left: return Command.MenuLeft;
                    case Key.D:
                    case Key.Right: return Command.MenuRight;
                }
                return Command.None;

            case InputEventJoypadButton pad when pad.Pressed:
                switch (pad.ButtonIndex)
                {
                    case JoyButton.Back: return Command.DummyToggle;
                    case JoyButton.Start: return Command.Menu;
                    case JoyButton.DpadUp: return Command.MenuUp;
                    case JoyButton.DpadDown: return Command.MenuDown;
                    case JoyButton.DpadLeft: return Command.MenuLeft;
                    case JoyButton.DpadRight: return Command.MenuRight;
                }
                return Command.None;

            case InputEventJoypadMotion motion when motion.Axis == JoyAxis.LeftY:
            {
                var id = (motion.Device, motion.Axis);
                LastAxis.TryGetValue(id, out var before);
                LastAxis[id] = motion.AxisValue;
                if (before > -Deadzone && motion.AxisValue <= -Deadzone) return Command.MenuUp;
                if (before < Deadzone && motion.AxisValue >= Deadzone) return Command.MenuDown;
                return Command.None;
            }

            default:
                return Command.None;
        }
    }

    /// <summary>How far the left stick must move before it walks (a little play so a resting stick stands still).</summary>
    private const float StickDeadzone = 0.2f;

    /// <summary>
    /// Which way the player is holding to walk right now, relative to the lock-on camera: forward is
    /// toward the opponent, right is to the right of the screen. W/A/S/D, the arrow keys, the D-pad
    /// or the left stick, on any pad. Keys and the D-pad walk at full speed; the stick walks as fast
    /// as it is pushed.
    /// </summary>
    public static MoveInput WalkInput()
    {
        float forward = 0, right = 0;
        if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up)) forward += 1;
        if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down)) forward -= 1;
        if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right)) right += 1;
        if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left)) right -= 1;

        foreach (int pad in Input.GetConnectedJoypads())
        {
            if (Input.IsJoyButtonPressed(pad, JoyButton.DpadUp)) forward += 1;
            if (Input.IsJoyButtonPressed(pad, JoyButton.DpadDown)) forward -= 1;
            if (Input.IsJoyButtonPressed(pad, JoyButton.DpadRight)) right += 1;
            if (Input.IsJoyButtonPressed(pad, JoyButton.DpadLeft)) right -= 1;
            var stick = new Vector2(Input.GetJoyAxis(pad, JoyAxis.LeftX), -Input.GetJoyAxis(pad, JoyAxis.LeftY));
            if (stick.Length() >= StickDeadzone)
            {
                // Rescale so the deadzone's edge is 0 and full tilt is 1.
                var scaled = stick.Normalized() * Mathf.Min(1f, (stick.Length() - StickDeadzone) / (1f - StickDeadzone));
                forward += scaled.Y;
                right += scaled.X;
            }
        }
        // Keys held together make a diagonal; the core caps the total at full speed.
        return new MoveInput(Mathf.RoundToInt(Mathf.Clamp(forward, -1, 1) * MoveInput.Full),
            Mathf.RoundToInt(Mathf.Clamp(right, -1, 1) * MoveInput.Full));
    }

    /// <summary>One line naming the connected pads, for the HUD and for setting up a pad.</summary>
    public static string PadStatus()
    {
        var pads = Input.GetConnectedJoypads();
        if (pads.Count == 0) return "No gamepad found · keyboard + mouse ready";
        var names = new List<string>();
        foreach (int pad in pads)
        {
            var known = Input.IsJoyKnown(pad) ? "" : " (unknown layout: buttons may be mixed up)";
            names.Add($"{Input.GetJoyName(pad)}{known}");
        }
        return "Gamepad: " + string.Join(", ", names) + " · keyboard + mouse also ready";
    }

    /// <summary>A short name for the last pad button pressed, to check the wiring while setting up.</summary>
    public static string? Describe(InputEvent e) => e switch
    {
        InputEventJoypadButton b when b.Pressed => $"pad button {(int)b.ButtonIndex} ({b.ButtonIndex})",
        InputEventJoypadMotion m when System.Math.Abs(m.AxisValue) >= Deadzone => $"pad axis {(int)m.Axis} ({m.Axis}) {(m.AxisValue > 0 ? "+" : "-")}",
        _ => null,
    };
}
