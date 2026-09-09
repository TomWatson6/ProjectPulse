using System;
using System.Collections.Generic;

namespace Pulse.Domain
{
    public readonly struct Solid
    {
        public readonly double Left, Right, Bottom, Top;
        public Solid(double left, double right, double bottom, double top)
        {
            if (!(right > left) || !(top > bottom)) throw new ArgumentException("Invalid solid bounds.");
            Left = left; Right = right; Bottom = bottom; Top = top;
        }
    }

    public readonly struct Spike
    {
        public readonly double X, Base, Width, Height;
        public Spike(double x, double baseY, double width = 1.05, double height = 1.0)
        {
            if (!(width > 0) || !(height > 0)) throw new ArgumentOutOfRangeException(nameof(width));
            X = x; Base = baseY; Width = width; Height = height;
        }
    }

    public sealed class LevelDefinition
    {
        public const double Bpm = 128;
        public const double BeatSeconds = 60.0 / Bpm;
        public const int BeatCount = 96;
        public const double Duration = BeatCount * BeatSeconds;
        public IReadOnlyList<Solid> Solids { get; }
        public IReadOnlyList<Spike> Spikes { get; }
        public IReadOnlyList<double> JumpBeats { get; }

        public LevelDefinition(Solid[] solids, Spike[] spikes, double[] jumpBeats)
        {
            Solids = Array.AsReadOnly((Solid[])solids.Clone());
            Spikes = Array.AsReadOnly((Spike[])spikes.Clone());
            JumpBeats = Array.AsReadOnly((double[])jumpBeats.Clone());
        }

        // An explicit authored score. These are cues, not a runtime pattern generator.
        public static LevelDefinition Afterlight(RunnerTuning tuning)
        {
            double[] beats = { 8,12,16,20,24,28,32,36,40,44,48,50,52,56,58,60,64,68,72,74,76,80,84,88,92 };
            double speed = tuning.Speed;
            double X(double beat) => beat * BeatSeconds * speed;
            var solids = new List<Solid>();
            // Three authored void crossings; uninterrupted floor everywhere else.
            double cursor = -30;
            foreach (double beat in new double[] { 28,60,84 })
            {
                double left = X(beat) + 1.25;
                solids.Add(new Solid(cursor, left, -6, 0));
                cursor = X(beat) + 4.75;
            }
            solids.Add(new Solid(cursor, X(100), -6, 0));
            // Raised landings introduce height without adding another movement mechanic.
            foreach (double beat in new double[] { 20,40,56,72 })
                solids.Add(new Solid(X(beat) + 1.85, X(beat) + 5.65, 0, 0.72));

            var spikes = new List<Spike>();
            foreach (double beat in beats)
            {
                if (beat == 28 || beat == 60 || beat == 84 || beat == 20 || beat == 40 || beat == 56 || beat == 72) continue;
                double center = X(beat) + 3.0;
                if (beat >= 48 && beat < 80)
                {
                    spikes.Add(new Spike(center - .57, 0, 1.05, 1.05));
                    spikes.Add(new Spike(center + .57, 0, 1.05, 1.05));
                }
                else spikes.Add(new Spike(center, 0));
            }
            return new LevelDefinition(solids.ToArray(), spikes.ToArray(), beats);
        }
    }
}
