using System;

namespace Shaolin36.Core
{
    /// <summary>
    /// A spot (or a direction) on the fight floor, seen from above: X and Z, in floor units of
    /// 1/<see cref="UnitsPerCm"/> cm. With 60 units to the centimetre and 60 ticks to the second, a speed
    /// of N cm per second is exactly N units per tick, so walking needs no leftover fractions.
    /// Whole numbers only, so the same inputs always give the same positions. Directions are worked
    /// out with + - * / and square roots only (all exact to the last bit on every machine), then
    /// rounded back to whole units.
    ///
    /// Axes are the engine's: X to the right of the start camera, Z toward it. Facing along
    /// a direction (x, z), your right hand points along (-z, x) (see <see cref="RightTurn"/>).
    /// </summary>
    public readonly struct FloorPoint : IEquatable<FloorPoint>
    {
        /// <summary>Floor units in one centimetre.</summary>
        public const int UnitsPerCm = Sim.TicksPerSecond;

        public FloorPoint(int x, int z)
        {
            X = x;
            Z = z;
        }

        /// <summary>X in floor units.</summary>
        public int X { get; }

        /// <summary>Z in floor units.</summary>
        public int Z { get; }

        public static FloorPoint Zero => default;

        /// <summary>A point given in whole centimetres.</summary>
        public static FloorPoint FromCm(int xCm, int zCm) => new FloorPoint(xCm * UnitsPerCm, zCm * UnitsPerCm);

        /// <summary>X in centimetres, rounded to the nearest whole one.</summary>
        public int XCm => ToCm(X);

        /// <summary>Z in centimetres, rounded to the nearest whole one.</summary>
        public int ZCm => ToCm(Z);

        public static int ToCm(int units) => (int)Math.Round(units / (double)UnitsPerCm, MidpointRounding.AwayFromZero);

        public static int ToCm(double units) => (int)Math.Round(units / UnitsPerCm, MidpointRounding.AwayFromZero);

        public bool IsZero => X == 0 && Z == 0;

        public long LengthSquared => (long)X * X + (long)Z * Z;

        /// <summary>Length in floor units.</summary>
        public double Length => Math.Sqrt(LengthSquared);

        /// <summary>The same direction turned a quarter to the right: (x, z) becomes (-z, x).</summary>
        public FloorPoint RightTurn => new FloorPoint(-Z, X);

        /// <summary>
        /// This direction scaled to <paramref name="units"/> long, each axis rounded to the nearest whole unit.
        /// A zero direction stays zero.
        /// </summary>
        public FloorPoint WithLength(double units)
        {
            if (IsZero) return Zero;
            double k = units / Length;
            return new FloorPoint(Round(X * k), Round(Z * k));
        }

        public static long Dot(FloorPoint a, FloorPoint b) => (long)a.X * b.X + (long)a.Z * b.Z;

        /// <summary>Distance between two points in whole centimetres (rounded).</summary>
        public static int DistanceCm(FloorPoint a, FloorPoint b) => ToCm((a - b).Length);

        internal static int Round(double v) => (int)Math.Round(v, MidpointRounding.AwayFromZero);

        public static FloorPoint operator +(FloorPoint a, FloorPoint b) => new FloorPoint(a.X + b.X, a.Z + b.Z);
        public static FloorPoint operator -(FloorPoint a, FloorPoint b) => new FloorPoint(a.X - b.X, a.Z - b.Z);
        public static FloorPoint operator -(FloorPoint a) => new FloorPoint(-a.X, -a.Z);
        public static bool operator ==(FloorPoint a, FloorPoint b) => a.Equals(b);
        public static bool operator !=(FloorPoint a, FloorPoint b) => !a.Equals(b);

        public bool Equals(FloorPoint other) => X == other.X && Z == other.Z;
        public override bool Equals(object? obj) => obj is FloorPoint p && Equals(p);
        public override int GetHashCode() => (X * 397) ^ Z;
        public override string ToString() => $"({XCm}, {ZCm}) cm";
    }
}
