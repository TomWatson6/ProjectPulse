using System;

namespace Pulse.Domain
{
    public struct PlayerState
    {
        public double X, Y, VelocityY;
        public bool Grounded, Dead, Complete;
    }

    public interface IMovementMode
    {
        void Integrate(ref PlayerState state, RunnerTuning tuning, bool jump);
    }

    public sealed class JumpMovement : IMovementMode
    {
        public void Integrate(ref PlayerState state, RunnerTuning tuning, bool jump)
        {
            if (jump) { state.VelocityY = tuning.JumpVelocity; state.Grounded = false; }
            state.Y += state.VelocityY * RunnerTuning.Step - 0.5 * tuning.Gravity * RunnerTuning.Step * RunnerTuning.Step;
            state.VelocityY -= tuning.Gravity * RunnerTuning.Step;
        }
    }

    public sealed class RunnerSimulation
    {
        private readonly RunnerTuning tuning;
        private readonly LevelDefinition level;
        private readonly IMovementMode movement;
        private int buffer, coyote;
        public PlayerState Player { get; private set; }
        public PlayerState Previous { get; private set; }
        public long Tick { get; private set; }
        public int JumpCount { get; private set; }
        public double Time => Tick * RunnerTuning.Step;
        public bool JumpedThisTick { get; private set; }
        public bool LandedThisTick { get; private set; }

        public RunnerSimulation(RunnerTuning tuning, LevelDefinition level, IMovementMode movement = null)
        {
            this.tuning = tuning; this.level = level;
            this.movement = movement ?? new JumpMovement(); Reset();
        }

        public void Reset()
        {
            Tick = 0; JumpCount = 0; buffer = 0; coyote = tuning.CoyoteTicks;
            JumpedThisTick = LandedThisTick = false;
            Player = Previous = new PlayerState { X = 0, Y = tuning.HalfHeight, Grounded = true };
        }

        public void Step(bool primaryPressed)
        {
            if (Player.Dead || Player.Complete) return;
            Previous = Player;
            var next = Player;
            Tick++;
            JumpedThisTick = LandedThisTick = false;
            if (primaryPressed) buffer = tuning.BufferTicks + 1;
            if (next.Grounded) coyote = tuning.CoyoteTicks + 1;
            bool jump = buffer > 0 && coyote > 0;
            if (jump) { buffer = coyote = 0; JumpCount++; JumpedThisTick = true; }
            if (buffer > 0) buffer--;
            if (coyote > 0) coyote--;

            next.X = tuning.Speed * Time;
            movement.Integrate(ref next,tuning,jump);
            next.Grounded = false;
            double startFoot = Previous.Y - tuning.HalfHeight;
            double endFoot = next.Y - tuning.HalfHeight;
            double bestTop = double.NegativeInfinity;
            // Resolve swept downward contacts at their time of impact, including raised platforms.
            foreach (var solid in level.Solids)
            {
                if (next.VelocityY > 0 || startFoot < solid.Top - 1e-8 || endFoot > solid.Top) continue;
                double fraction = Math.Abs(startFoot-endFoot) < 1e-12 ? 0 : (startFoot-solid.Top)/(startFoot-endFoot);
                double contactX = Previous.X + (next.X-Previous.X)*fraction;
                if (contactX + tuning.HalfWidth > solid.Left && contactX - tuning.HalfWidth < solid.Right
                    && next.X + tuning.HalfWidth > solid.Left && next.X - tuning.HalfWidth < solid.Right)
                    bestTop = Math.Max(bestTop, solid.Top);
            }
            if (!double.IsNegativeInfinity(bestTop))
            {
                next.Y = bestTop + tuning.HalfHeight; next.VelocityY = 0; next.Grounded = true;
                LandedThisTick = !Previous.Grounded;
            }
            // Sides and undersides are solid, never pass-through. A runner hitting a wall dies.
            foreach (var solid in level.Solids)
            {
                if (next.X+tuning.HalfWidth > solid.Left+1e-8 && next.X-tuning.HalfWidth < solid.Right-1e-8
                    && next.Y-tuning.HalfHeight < solid.Top-1e-8 && next.Y+tuning.HalfHeight > solid.Bottom+1e-8)
                    next.Dead = true;
            }
            foreach (var spike in level.Spikes)
            {
                if (spike.X+spike.Width/2 < Previous.X-tuning.HalfWidth || spike.X-spike.Width/2 > next.X+tuning.HalfWidth) continue;
                if (Collision.SweepSpike(Previous.X,Previous.Y,next.X,next.Y,tuning.HalfWidth,tuning.HalfHeight,spike)) next.Dead = true;
            }
            if (next.Y < -4) next.Dead = true;
            if (!next.Dead && Time >= LevelDefinition.Duration) next.Complete = true;
            Player = next;
        }
    }
}
