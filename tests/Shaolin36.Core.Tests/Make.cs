using Shaolin36.Core.Data;

namespace Shaolin36.Core.Tests;

/// <summary>Builds techniques for tests with readable defaults.</summary>
internal static class Make
{
    public static Technique Move(
        string id = "test",
        Weight weight = Weight.Medium,
        bool weapon = false,
        int startup = 10,
        int active = 3,
        int recovery = 12,
        int window = 4,
        int perfect = 2,
        bool clashable = true,
        Category category = Category.Waigong,
        int hitstun = 18,
        int reach = 120,
        string next = "",
        int chain = 0,
        int perfectParry = 0,
        int stamina = 5,
        int damage = 5) =>
        new(id, id, category, weight, weapon, startup, active, recovery, window, perfect, stamina, 0, damage, clashable,
            hitstun, reach, next, chain, perfectParry);

    /// <summary>A parry like the sheet's: startup 3, a 10-frame window (frames 3-12), perfect on frames 3-6.</summary>
    public static Technique ParryMove(int stamina = 10) =>
        Move("parry", Weight.Light, startup: 3, active: 10, recovery: 20, window: 0, perfect: 0, clashable: false,
            category: Category.Huajin, hitstun: 0, reach: 0, perfectParry: 4, stamina: stamina);

    /// <summary>
    /// Every rules key with a distinct value (its position in <see cref="Rules.Keys"/> plus 101),
    /// so a test can tell which key a number came from. Pass overrides to change some.
    /// </summary>
    public static string RulesText(params (string key, string value)[] overrides)
    {
        var sb = new System.Text.StringBuilder("key,value,note\n");
        for (int i = 0; i < Rules.Keys.Length; i++)
        {
            var key = Rules.Keys[i];
            var value = (101 + i).ToString();
            foreach (var (k, v) in overrides) if (k == key) value = v;
            sb.Append(key).Append(',').Append(value).Append(",test\n");
        }
        return sb.ToString();
    }

    public static int RuleValue(string key) => 101 + System.Array.IndexOf(Rules.Keys, key);

    public static Rules TestRules()
    {
        // exhausted_until must not exceed stamina_max, and qi_start must not exceed qi_max.
        var r = RulesCsv.Parse(RulesText(("exhausted_until", "50"), ("qi_start", "0")));
        if (!r.Ok) throw new System.InvalidOperationException(string.Join("\n", r.Errors));
        return r.Rules!;
    }
}
