using System;
using System.IO;
using Pulse.Domain;

namespace Pulse.Editor
{
    /// <summary>Original fixed composition, rendered once into a PCM WAV by the editor.</summary>
    internal static class AfterlightComposer
    {
        private const int Rate=44100;
        private static float[] left,right;
        private static uint noise=0xA17E12u;
        private static readonly int[] Roots={45,41,48,43};
        private static readonly int[] Motif={12,19,15,22,19,12,10,15};
        private static double Frequency(int midi) => 440*Math.Pow(2,(midi-69)/12.0);
        private static double Random()
        { noise^=noise<<13; noise^=noise>>17; noise^=noise<<5; return noise/(double)uint.MaxValue*2-1; }

        public static void Write(string path)
        {
            int samples=(int)Math.Round(LevelDefinition.Duration*Rate);
            left=new float[samples]; right=new float[samples]; noise=0xA17E12u;
            double beat=LevelDefinition.BeatSeconds;
            for(int bar=0;bar<24;bar++)
            {
                int root=Roots[(bar/2)%4], third=(bar/2)%4==0?3:4;
                double start=bar*4*beat;
                Pad(start,root+12,4.5*beat,.047,-.3);
                Pad(start,root+12+third,4.5*beat,.036,.3);
                Pad(start,root+19,4.5*beat,.037,0);
            }
            for(int b=0;b<96;b++)
            {
                if(b==47) continue;
                bool drop=b>=48 && b<80;
                bool build=b>=32 && b<47;
                bool outro=b>=88;
                int root=Roots[(b/8)%4];
                if(!outro || b%2==0) Drum(b*beat,0,drop?.73:.58);
                if(b%2==1 && b>=8) Drum(b*beat,1,drop?.28:.18);
                Drum((b+.5)*beat,2,drop?.16:.10);
                if(drop || build) { Drum((b+.25)*beat,2,.05); Drum((b+.75)*beat,2,.065); }
                Note(b*beat,root,.36,drop?.22:.16,0,0);
                if(drop) Note((b+.75)*beat,root+12,.13,.09,.12,0);
                for(int sub=0;sub<2;sub++)
                {
                    int degree=Motif[(b*2+sub)%Motif.Length];
                    // Adjust the colour tone of major chords; retain an original repeating contour.
                    if(degree==15 && (b/8)%4!=0) degree=16;
                    Note((b+sub*.5)*beat,root+degree+12,.46,b<8?.10:drop?.14:.105,sub==0?-.4:.4,1);
                }
                if(drop && b%2==0) Note(b*beat,root+31,.8,.075,.15,2);
                if(build && b>=40)
                {
                    int count=b>=44?4:2;
                    for(int j=0;j<count;j++) Drum((b+j/(double)count)*beat,1,.05+(b-40)*.012);
                }
            }
            Sweep(40*beat,8*beat);
            Drum(48*beat,3,.48);
            // A short stereo echo, limited to two taps, gives space without blurring the beat.
            int delay=(int)(beat*.75*Rate);
            for(int i=samples-1;i>=delay;i--)
            {
                float a=left[i-delay], b=right[i-delay];
                left[i]+=b*.16f; right[i]+=a*.16f;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using(var stream=new BinaryWriter(File.Create(path)))
            {
                stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); stream.Write(36+samples*4);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); stream.Write(16);
                stream.Write((short)1); stream.Write((short)2); stream.Write(Rate); stream.Write(Rate*4);
                stream.Write((short)4); stream.Write((short)16);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("data")); stream.Write(samples*4);
                for(int i=0;i<samples;i++)
                {
                    double fade=Math.Min(1,i/(Rate*.012))*Math.Min(1,(samples-i)/(Rate*.7));
                    stream.Write((short)(Math.Tanh(left[i]*1.15)*.86*fade*32767));
                    stream.Write((short)(Math.Tanh(right[i]*1.15)*.86*fade*32767));
                }
            }
            left=right=null;
        }

        private static void Add(int i,double value,double pan)
        {
            if(i<0 || i>=left.Length) return;
            left[i]+=(float)(value*(.8-.3*pan)); right[i]+=(float)(value*(.8+.3*pan));
        }
        private static void Note(double start,int midi,double duration,double amplitude,double pan,int kind)
        {
            double f=Frequency(midi); int offset=(int)Math.Round(start*Rate);
            for(int n=0;n<duration*Rate;n++)
            {
                double t=n/(double)Rate;
                double env=(1-Math.Exp(-t*700))*Math.Exp(-t*(kind==0?8:5))*Math.Min(1,(duration-t)*40);
                double phase=2*Math.PI*f*t;
                double tone=Math.Sin(phase)+.25*Math.Sin(phase*2)+.09*Math.Sin(phase*3);
                if(kind==1) tone+=.16*Math.Sin(phase*1.003)+.10*Math.Sin(phase*4)*Math.Exp(-t*14);
                if(kind==2) tone=Math.Sin(phase)+.25*Math.Sin(phase*.998);
                Add(offset+n,tone*env*amplitude,pan);
            }
        }
        private static void Pad(double start,int midi,double duration,double amplitude,double pan)
        {
            double f=Frequency(midi); int offset=(int)Math.Round(start*Rate);
            for(int n=0;n<duration*Rate;n++)
            {
                double t=n/(double)Rate, songTime=start+t;
                double env=Math.Min(1,t/.25)*Math.Min(1,(duration-t)/.6);
                double phase=2*Math.PI*f*t;
                double sidechain=.52+.48*(1-Math.Exp(-(songTime%LevelDefinition.BeatSeconds)*9));
                double silence=songTime>=47*LevelDefinition.BeatSeconds && songTime<48*LevelDefinition.BeatSeconds?.12:1;
                Add(offset+n,(Math.Sin(phase)+.35*Math.Sin(phase*1.002)+.15*Math.Sin(phase*2))*env*amplitude*sidechain*silence,pan);
            }
        }
        private static void Drum(double start,int kind,double amplitude)
        {
            int offset=(int)Math.Round(start*Rate); double duration=kind==0?.45:kind==1?.22:kind==2?.09:1.8;
            double phase=0, previousNoise=0;
            for(int n=0;n<duration*Rate;n++)
            {
                double t=n/(double)Rate, noiseSample=Random();
                double high=noiseSample-previousNoise*.8; previousNoise=noiseSample;
                double sample;
                if(kind==0)
                {
                    phase+=2*Math.PI*(48+115*Math.Exp(-t*35))/Rate;
                    sample=Math.Sin(phase)*Math.Exp(-t*11)+noiseSample*Math.Exp(-t*260)*.15;
                }
                else if(kind==1) sample=high*Math.Exp(-t*21)*.62+Math.Sin(t*2*Math.PI*180)*Math.Exp(-t*28)*.38;
                else if(kind==2) sample=high*Math.Exp(-t*55)*.6;
                else sample=high*Math.Exp(-t*3)*.5+Math.Sin(t*2*Math.PI*45)*Math.Exp(-t*5);
                Add(offset+n,sample*amplitude,kind==2?.24:0);
            }
        }
        private static void Sweep(double start,double duration)
        {
            int offset=(int)(start*Rate); double filtered=0;
            for(int n=0;n<duration*Rate;n++)
            {
                double t=n/(double)Rate, u=t/duration;
                filtered+=(Random()-filtered)*(.015+u*.2);
                double fade=Math.Min(1,(duration-t)*35);
                Add(offset+n,(filtered*.14+Math.Sin(2*Math.PI*(180*t+200*t*t))*.015)*u*u*fade,Math.Sin(t)*.6);
            }
        }
    }
}
