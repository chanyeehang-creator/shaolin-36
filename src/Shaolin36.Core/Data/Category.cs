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
        /// <summary>化劲: parries, guards and counters.</summary>
        Huajin,
        /// <summary>擒拿: grabs, command grabs, struggle prompts.</summary>
        Qinna,
        /// <summary>气功: self-buffs and heals. Costs Qi.</summary>
        Qigong,
    }

    /// <summary>Clash weight: heavy beats medium beats light.</summary>
    public enum Weight
    {
        Light = 0,
        Medium = 1,
        Heavy = 2,
    }
}
