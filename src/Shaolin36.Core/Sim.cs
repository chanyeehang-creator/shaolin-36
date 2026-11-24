namespace Shaolin36.Core
{
    /// <summary>
    /// The simulation runs on a fixed tick. All frame data in the sheets is
    /// counted in these ticks, so "10 frames" always means 10/60 of a second,
    /// whatever the render frame rate is.
    /// </summary>
    public static class Sim
    {
        public const int TicksPerSecond = 60;

        public static double FramesToSeconds(int frames) => frames / (double)TicksPerSecond;
    }
}
