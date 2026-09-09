using System;

namespace Pulse.Domain
{
    /// <summary>A monotonic audio-clock mapping. No frame time or Unity dependency.</summary>
    public sealed class SongClock
    {
        private double epoch, offset, last;
        public bool Paused { get; private set; } = true;
        public double ScheduledStart => epoch;

        public void Start(double dspNow, double leadSeconds, double position = 0)
        {
            if (!Finite(dspNow) || !Finite(leadSeconds) || !Finite(position) || leadSeconds < 0 || position < 0)
                throw new ArgumentOutOfRangeException(nameof(dspNow));
            epoch = dspNow + leadSeconds;
            offset = last = position;
            Paused = false;
        }

        public double Read(double dspNow)
        {
            if (!Finite(dspNow)) throw new ArgumentOutOfRangeException(nameof(dspNow));
            if (!Paused) last = Math.Max(last, offset + Math.Max(0, dspNow - epoch));
            return last;
        }

        public void Pause(double dspNow) { Read(dspNow); Paused = true; }
        public void Resume(double dspNow, double leadSeconds) { Start(dspNow, leadSeconds, last); }
        public void Reset() { epoch = offset = last = 0; Paused = true; }
        private static bool Finite(double v) => !double.IsInfinity(v) && !double.IsNaN(v);
    }
}
