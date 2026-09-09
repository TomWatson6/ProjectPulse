using System;
using System.Collections.Generic;
using Pulse.Domain;

internal static class Program
{
    private static int failed, passed;
    private static readonly RunnerTuning Tuning = new RunnerTuning();
    private static readonly LevelDefinition Chart = LevelDefinition.Afterlight(Tuning);

    private static void Main()
    {
        Test("Clock scheduled start, no early motion", () => {
            var c = new SongClock(); c.Start(10,.1); Equal(c.Read(10.05),0); Equal(c.Read(10.6),.5); });
        Test("Clock monotonic under jitter", () => {
            var c = new SongClock(); c.Start(0,0); Equal(c.Read(2),2); Equal(c.Read(1),2); });
        Test("Pause and scheduled resume preserve exact position", () => {
            var c = new SongClock(); c.Start(1,.1); c.Pause(3.1); Equal(c.Read(90),2);
            c.Resume(100,.1); Equal(c.Read(100.05),2); Equal(c.Read(100.6),2.5); });
        Test("Clock restart discards old epoch and offset", () => {
            var c = new SongClock(); c.Start(0,0); c.Read(25); c.Start(50,.1); Equal(c.Read(50),0); Equal(c.Read(51.1),1);
            c.Reset(); Check(c.Paused); Equal(c.Read(100),0); });
        Test("Configuration rejects non-finite and invalid values", () => {
            Throws(() => new RunnerTuning(gravity:0)); Throws(() => new RunnerTuning(speed:double.NaN));
            Throws(() => new RunnerTuning(jumpVelocity:double.PositiveInfinity)); Throws(() => new RunnerTuning(bufferTicks:-1));
            Throws(() => new SongClock().Start(0,-1)); });
        Test("Session guards invalid transitions", () => {
            var s = new RunSession(); s.Die(); Check(s.State == RunState.Ready); s.Start(); s.Pause(); s.Complete();
            Check(s.State == RunState.Paused); s.Resume(); s.Die(); s.Resume(); Check(s.State == RunState.Dead);
            s.Start(); Check(s.Attempt == 2); s.Complete(); Check(s.State == RunState.Complete); s.ReturnToTitle(); Check(s.State == RunState.Ready); });
        Test("Input timestamps do not apply to old catch-up ticks", () => {
            var q = new InputTimeline(); q.Enqueue(1,10); Check(!q.Consume(239)); Check(q.Consume(240)); Check(!q.Consume(241));
            q.Enqueue(1,300); Check(q.Consume(301)); q.Enqueue(2,400); q.Clear(); Check(!q.Consume(999)); });
        Test("Grounded motion is stable for thousands of ticks", () => {
            var s = Flat(); for(int i=0;i<5000;i++) s.Step(false);
            Equal(s.Player.Y,Tuning.HalfHeight); Equal(s.Player.X,s.Time*Tuning.Speed); Check(s.Player.Grounded && !s.Player.Dead); });
        Test("Jump trajectory matches analytic ballistic motion", () => {
            var s = Flat(); s.Step(true); for(int i=1;i<72;i++) s.Step(false);
            double t = 72*RunnerTuning.Step; Equal(s.Player.Y,Tuning.HalfHeight+Tuning.JumpVelocity*t-.5*Tuning.Gravity*t*t);
            Check(s.JumpCount==1 && !s.Player.Grounded); });
        Test("Mid-air press does not grant double jump", () => {
            var s = Flat(); s.Step(true); for(int i=0;i<50;i++) s.Step(false); s.Step(true); Check(s.JumpCount==1); });
        Test("Input buffer jumps immediately after landing", () => {
            var s = Flat(); s.Step(true); for(int i=1;i<164;i++) s.Step(false); s.Step(true);
            for(int i=0;i<12;i++) s.Step(false); Check(s.JumpCount==2 && !s.Player.Grounded); });
        Test("Triangle collision catches crossing and allows clear overhead", () => {
            var spike = new Spike(5,0); Check(Collision.SweepSpike(0,.34,10,.34,.3,.34,spike));
            Check(!Collision.SweepSpike(0,2,10,2,.3,.34,spike));
            Check(!Collision.SweepSpike(4.2,.9,4.2,.9,.1,.1,spike)); });
        Test("No-input replay dies at the first obstacle", () => {
            var s = new RunnerSimulation(Tuning,Chart); for(int i=0;i<2000 && !s.Player.Dead;i++) s.Step(false);
            Check(s.Player.Dead && s.Time>3.7 && s.Time<4.2); });
        Test("Authored 45-second reference replay completes", () => { var s = Replay(0,60); Check(s.Player.Complete,Describe(s)); Check(s.JumpCount==Chart.JumpBeats.Count); });
        Test("Authored replay tolerates 35ms early and late input", () => {
            Check(Replay(-.035,60).Player.Complete,Describe(Replay(-.035,60)));
            Check(Replay(.035,60).Player.Complete,Describe(Replay(.035,60))); });
        Test("Identical replay is invariant to render frame grouping", () => {
            var a=Replay(0,30); var b=Replay(0,144); var c=Replay(0,59);
            Check(a.Player.Complete && b.Player.Complete && c.Player.Complete);
            Check(a.Tick==b.Tick && b.Tick==c.Tick); Equal(a.Player.X,b.Player.X,0); Equal(a.Player.Y,c.Player.Y,0); });
        Test("Restart resets movement, pending jump and death", () => {
            var s = new RunnerSimulation(Tuning,Chart); for(int i=0;i<1500;i++) s.Step(false); Check(s.Player.Dead);
            s.Reset(); Check(s.Tick==0 && s.JumpCount==0 && !s.Player.Dead && s.Player.Grounded);
            var fresh = new RunnerSimulation(Tuning,Chart);
            for(int i=0;i<1000;i++) { bool input=i==900; s.Step(input); fresh.Step(input); Equal(s.Player.Y,fresh.Player.Y,0); } });
        Test("Completed simulation cannot advance", () => { var s=Replay(0,60); long tick=s.Tick; s.Step(true); Check(s.Tick==tick); });
        Test("Authored gaps cause falls when their jump is omitted", () => {
            foreach(double beat in new double[]{28,60,84}) { var s=Replay(0,60,beat); Check(s.Player.Dead,Describe(s)); } });
        Test("Visual timeline seeks and restarts without stale drop state", () => {
            var track=VisualEventTrack.Afterlight(); Check(track.At(47.9).Phase==VisualPhase.Silence);
            Check(track.At(48).Phase==VisualPhase.Afterlight); Check(track.At(0).Phase==VisualPhase.Arrival);
            Check(track.At(88).Phase==VisualPhase.Home); Throws(() => new VisualEventTrack(new VisualEvent(1,VisualPhase.Flow,1))); });
        Test("Quality profiles scale decoration without changing gameplay", () => {
            var low=new GraphicsQualityProfile(GraphicsTier.Low); var high=new GraphicsQualityProfile(GraphicsTier.Ultra);
            Check(low.ParticleCapacity<high.ParticleCapacity && low.DustCount<high.DustCount && !low.Glow && high.Glow);
            Throws(() => new GraphicsQualityProfile((GraphicsTier)99)); Check(Replay(0,60).Player.Complete); });
        Test("Coyote time permits a late ledge jump but then expires", () => {
            var ledge=new LevelDefinition(new[]{new Solid(-5,.2,-6,0)},Array.Empty<Spike>(),Array.Empty<double>());
            var s=new RunnerSimulation(Tuning,ledge); while(s.Player.Grounded) s.Step(false);
            for(int i=0;i<4;i++) s.Step(false); s.Step(true); Check(s.JumpCount==1);
            s.Reset(); while(s.Player.Grounded) s.Step(false); for(int i=0;i<15;i++) s.Step(false);
            s.Step(true); Check(s.JumpCount==0); });
        Test("A solid wall blocks the runner", () => {
            var wall=new LevelDefinition(new[]{new Solid(-5,50,-6,0),new Solid(3,4,0,5)},Array.Empty<Spike>(),Array.Empty<double>());
            var s=new RunnerSimulation(Tuning,wall); for(int i=0;i<200 && !s.Player.Dead;i++) s.Step(false);
            Check(s.Player.Dead && s.Player.X<3); });
        Test("Authored raised platforms provide genuine elevated landings", () => {
            var s=new RunnerSimulation(Tuning,Chart); bool landed=false;
            var inputs=new HashSet<long>(); foreach(double beat in Chart.JumpBeats) inputs.Add((long)Math.Round(beat*LevelDefinition.BeatSeconds*RunnerTuning.TickRate));
            while(!s.Player.Dead && !s.Player.Complete) { s.Step(inputs.Contains(s.Tick+1)); if(s.Player.Grounded && s.Player.Y>Tuning.HalfHeight+.5) landed=true; }
            Check(landed && s.Player.Complete); });
        Console.WriteLine($"\n{passed} passed, {failed} failed.");
        Environment.ExitCode=failed==0 ? 0 : 1;
    }

    private static RunnerSimulation Flat() => new RunnerSimulation(Tuning,new LevelDefinition(new[]{new Solid(-20,1000,-6,0)},Array.Empty<Spike>(),Array.Empty<double>()));
    private static RunnerSimulation Replay(double shift,int fps,double skip=-1)
    {
        var s = new RunnerSimulation(Tuning,Chart); var inputs=new HashSet<long>();
        foreach(var beat in Chart.JumpBeats) if(beat!=skip) inputs.Add((long)Math.Round((beat*LevelDefinition.BeatSeconds+shift)*RunnerTuning.TickRate));
        for(int frame=1;frame<=fps*46 && !s.Player.Dead && !s.Player.Complete;frame++)
        {
            long target=(long)Math.Floor((double)frame/fps*RunnerTuning.TickRate+1e-8);
            while(s.Tick<target && !s.Player.Dead && !s.Player.Complete) s.Step(inputs.Contains(s.Tick+1));
        }
        return s;
    }
    private static string Describe(RunnerSimulation s) => $"t={s.Time:F4} x={s.Player.X:F3} y={s.Player.Y:F3} dead={s.Player.Dead} jumps={s.JumpCount}";
    private static void Test(string name,Action body) { try { body(); passed++; Console.WriteLine("PASS "+name); } catch(Exception e) { failed++; Console.WriteLine("FAIL "+name+": "+e.Message); } }
    private static void Check(bool value,string message="Assertion failed") { if(!value) throw new Exception(message); }
    private static void Equal(double actual,double expected,double epsilon=1e-8) { Check(Math.Abs(actual-expected)<=epsilon,$"Expected {expected}, got {actual}"); }
    private static void Throws(Action body) { try { body(); } catch(ArgumentException) { return; } throw new Exception("Expected configuration rejection"); }
}
