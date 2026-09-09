using System;
using System.Collections.Generic;

namespace Pulse.Domain
{
    public readonly struct GameplayActions
    {
        public readonly bool PrimaryAction, Pause, Restart;
        public GameplayActions(bool primaryAction = false, bool pause = false, bool restart = false)
        { PrimaryAction = primaryAction; Pause = pause; Restart = restart; }
    }

    /// <summary>Physical events are assigned to a simulation tick, never retroactively to a stalled frame.</summary>
    public sealed class InputTimeline
    {
        private readonly Queue<long> presses = new Queue<long>();
        private long lastQueued;
        public void Enqueue(double songTime, long simulatedTick)
        {
            long tick = Math.Max(simulatedTick + 1, (long)Math.Ceiling(songTime * RunnerTuning.TickRate));
            tick = Math.Max(lastQueued, tick);
            presses.Enqueue(tick); lastQueued = tick;
        }
        public bool Consume(long tick)
        {
            bool pressed = false;
            while (presses.Count > 0 && presses.Peek() <= tick) { presses.Dequeue(); pressed = true; }
            return pressed;
        }
        public void Clear() { presses.Clear(); lastQueued = 0; }
    }

    public enum RunState { Ready, Playing, Paused, Dead, Complete }

    public sealed class RunSession
    {
        public RunState State { get; private set; } = RunState.Ready;
        public int Attempt { get; private set; }
        public void Start() { Attempt++; State = RunState.Playing; }
        public void Pause() { if (State == RunState.Playing) State = RunState.Paused; }
        public void Resume() { if (State == RunState.Paused) State = RunState.Playing; }
        public void Die() { if (State == RunState.Playing) State = RunState.Dead; }
        public void Complete() { if (State == RunState.Playing) State = RunState.Complete; }
        public void ReturnToTitle() { State = RunState.Ready; }
    }
}
