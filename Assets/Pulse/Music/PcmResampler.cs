using System;
using System.Threading;

namespace Pulse.Music
{
    public static class PcmResampler
    {
        /// <summary>Windowed-sinc low-pass polyphase resampling; no alias-producing sample skipping.</summary>
        public static float[] ToAnalysisRate(float[] source,int rate,CancellationToken cancellation)
        {
            if(rate==AudioAnalyser.AnalysisRate) return source;
            if(rate<8000 || rate>192000 || source==null || source.Length==0) throw new ArgumentException("Invalid PCM.");
            const int phases=512,taps=48,half=taps/2;
            var kernel=new double[phases,taps]; double cutoff=Math.Min(1,(double)AudioAnalyser.AnalysisRate/rate)*.94;
            for(int p=0;p<phases;p++)
            {
                double sum=0;
                for(int k=0;k<taps;k++)
                {
                    double x=k-half+1-(double)p/phases;
                    double value=(Math.Abs(x)<1e-9?cutoff:Math.Sin(Math.PI*x*cutoff)/(Math.PI*x))*(.5+.5*Math.Cos(Math.PI*x/half));
                    kernel[p,k]=value; sum+=value;
                }
                for(int k=0;k<taps;k++) kernel[p,k]/=sum;
            }
            int count=(int)Math.Round((double)source.Length*AudioAnalyser.AnalysisRate/rate); var output=new float[count];
            for(int i=0;i<count;i++)
            {
                if(i%8192==0) cancellation.ThrowIfCancellationRequested();
                double position=(double)i*rate/AudioAnalyser.AnalysisRate; int center=(int)position,phase=(int)((position-center)*phases); double value=0;
                for(int k=0;k<taps;k++) { int j=center-half+1+k; if(j>=0 && j<source.Length) value+=source[j]*kernel[phase,k]; }
                output[i]=(float)Math.Max(-1,Math.Min(1,value));
            }
            return output;
        }
    }
}
