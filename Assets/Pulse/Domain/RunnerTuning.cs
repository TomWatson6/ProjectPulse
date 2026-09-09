using System;

namespace Pulse.Domain
{
    public sealed class RunnerTuning
    {
        public const int TickRate = 240;
        public const double Step = 1.0 / TickRate;
        public double Speed { get; }
        public double JumpVelocity { get; }
        public double Gravity { get; }
        public double HalfWidth { get; }
        public double HalfHeight { get; }
        public int BufferTicks { get; }
        public int CoyoteTicks { get; }

        public RunnerTuning(double speed = 9, double jumpVelocity = 12.8, double gravity = 36,
            double halfWidth = 0.30, double halfHeight = 0.34, int bufferTicks = 19, int coyoteTicks = 10)
        {
            if (!Positive(speed) || !Positive(jumpVelocity) || !Positive(gravity) ||
                !Positive(halfWidth) || !Positive(halfHeight) || bufferTicks < 0 || coyoteTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(speed), "Movement tuning must be finite and positive; grace ticks nonnegative.");
            Speed = speed; JumpVelocity = jumpVelocity; Gravity = gravity;
            HalfWidth = halfWidth; HalfHeight = halfHeight;
            BufferTicks = bufferTicks; CoyoteTicks = coyoteTicks;
        }

        private static bool Positive(double value) => value > 0 && !double.IsInfinity(value) && !double.IsNaN(value);
    }
}
