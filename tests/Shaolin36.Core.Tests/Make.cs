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
        Category category = Category.Waigong) =>
        new(id, id, category, weight, weapon, startup, active, recovery, window, perfect, 5, 0, 5, clashable);
}
