using Pulse.Rendering;
using UnityEngine;

namespace Pulse.Visuals
{
    public sealed class ParticleField
    {
        private struct Particle { public Vector2 Position,Velocity; public float Birth,Life,Size; public Color Color; }
        private readonly Particle[] particles=new Particle[260];
        private int cursor;
        public void Clear() { for(int i=0;i<particles.Length;i++) particles[i].Life=0; cursor=0; }
        public void Burst(Vector2 position,Color color,int count,float now,int capacity,float strength=1)
        {
            for(int i=0;i<count;i++)
            {
                float angle=(i*2.399963f+cursor*.41f);
                float velocity=(1.1f+(i%7)*.4f)*strength;
                particles[cursor%capacity]=new Particle {
                    Position=position,Velocity=new Vector2(Mathf.Cos(angle)*velocity,Mathf.Sin(angle)*velocity+1.5f),
                    Birth=now,Life=.45f+(i%5)*.10f,Size=.025f+(i%3)*.017f,Color=color };
                cursor++;
            }
        }
        public void Render(MeshCanvas canvas,float now,int capacity,bool glow)
        {
            for(int i=0;i<capacity;i++)
            {
                Particle p=particles[i]; float age=now-p.Birth;
                if(p.Life<=0 || age<0 || age>p.Life) continue;
                float alpha=1-age/p.Life;
                Vector2 position=p.Position+p.Velocity*age+Vector2.down*(age*age*2);
                Color color=p.Color; color.a=alpha;
                canvas.Line(position,position-p.Velocity*.035f,p.Size*alpha,color);
                if(glow) { color.a*=.25f; canvas.Glow(position,p.Size*5,color); }
            }
        }
    }
}
