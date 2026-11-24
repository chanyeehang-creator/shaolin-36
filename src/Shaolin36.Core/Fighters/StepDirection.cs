namespace Shaolin36.Core.Fighters
{
    /// <summary>
    /// The four ways a quick step can go, relative to the lock-on: forward is toward the other fighter,
    /// right is round their side to your right.
    /// </summary>
    public enum StepDirection
    {
        None = 0,
        Forward,
        Back,
        Left,
        Right,
    }
}
