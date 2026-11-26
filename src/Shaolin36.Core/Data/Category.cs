namespace Shaolin36.Core.Data
{
    /// <summary>The 7 move categories (七十二绝技). Each does one job in a fight.</summary>
    public enum Category
    {
        /// <summary>外功: combo strings, main damage. Costs stamina.</summary>
        Waigong,
        /// <summary>内功: one heavy strike or Qi blast; stuns and guard breaks.</summary>
        Neigong,
        /// <summary>轻功: dodges, jumps, dashes and dash attacks.</summary>
        Qinggong,
        /// <summary>点穴: debuffs. Costs Qi.</summary>
        Dianxue,
        /// <summary>防身 (was 化劲): parries, guards and counters. The sheet still accepts "huajin" / "化劲".</summary>
        Fangshen,
        /// <summary>擒拿: grabs, command grabs, struggle prompts.</summary>
        Qinna,
        /// <summary>炼气: self-buffs and heals. Costs Qi.</summary>
        Lianqi,
    }

    /// <summary>
    /// Clash weight: heavy beats medium beats light. <see cref="Energy"/> is a class of its own (pure energy, such as
    /// 狮吼功): no physical move can clash it and it clashes no physical move, but two energy moves that meet cancel out.
    /// </summary>
    public enum Weight
    {
        Light = 0,
        Medium = 1,
        Heavy = 2,
        /// <summary>Pure energy (狮吼功): can't be clashed by light, medium or heavy; cancels another energy move. Can be blocked, never parried.</summary>
        Energy = 3,
    }
}
