using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pulse.Rendering
{
    /// <summary>One reusable mesh per layer. No per-obstacle objects, colliders, lights or materials.</summary>
    public sealed class MeshCanvas : MonoBehaviour
    {
        private readonly List<Vector3> vertices=new List<Vector3>(18000);
        private readonly List<Color> colors=new List<Color>(18000);
        private readonly List<Vector3> uv=new List<Vector3>(18000);
        private readonly List<int> indices=new List<int>(30000);
        private Mesh mesh;
        private Material material;

        public static MeshCanvas Create(string name,int order,Transform parent)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false);
            var canvas=go.AddComponent<MeshCanvas>();
            canvas.mesh=new Mesh { name=name, indexFormat=IndexFormat.UInt32 }; canvas.mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh=canvas.mesh;
            canvas.material=new Material(Resources.Load<Shader>("PulseUnlit"));
            var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=canvas.material;
            renderer.sortingOrder=order; renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            return canvas;
        }
        public void Begin() { vertices.Clear(); colors.Clear(); uv.Clear(); indices.Clear(); }
        public void End()
        {
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0,uv);
            mesh.SetTriangles(indices,0); mesh.RecalculateBounds();
        }
        private void Vertex(Vector2 p,Color color,Vector3 tex)
        { vertices.Add(new Vector3(p.x,p.y,0)); colors.Add(color); uv.Add(tex); }
        public void Triangle(Vector2 a,Vector2 b,Vector2 c,Color color)
        {
            int i=vertices.Count; Vertex(a,color,Vector3.zero); Vertex(b,color,Vector3.zero); Vertex(c,color,Vector3.zero);
            indices.Add(i); indices.Add(i+1); indices.Add(i+2);
        }
        public void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color bottom,Color top,bool glow=false)
        {
            int i=vertices.Count; float g=glow?1:0;
            Vertex(a,bottom,new Vector3(-1,-1,g)); Vertex(b,bottom,new Vector3(1,-1,g));
            Vertex(c,top,new Vector3(1,1,g)); Vertex(d,top,new Vector3(-1,1,g));
            indices.Add(i); indices.Add(i+1); indices.Add(i+2); indices.Add(i); indices.Add(i+2); indices.Add(i+3);
        }
        public void Rect(float x,float y,float width,float height,Color color)
            => Quad(new Vector2(x,y),new Vector2(x+width,y),new Vector2(x+width,y+height),new Vector2(x,y+height),color,color);
        public void Gradient(float x,float y,float width,float height,Color bottom,Color top)
            => Quad(new Vector2(x,y),new Vector2(x+width,y),new Vector2(x+width,y+height),new Vector2(x,y+height),bottom,top);
        public void Glow(Vector2 p,float radius,Color color)
            => Quad(p+new Vector2(-radius,-radius),p+new Vector2(radius,-radius),p+new Vector2(radius,radius),p+new Vector2(-radius,radius),color,color,true);
        public void Line(Vector2 a,Vector2 b,float width,Color color)
        {
            var n=new Vector2(-(b.y-a.y),b.x-a.x).normalized*width*.5f;
            Quad(a-n,b-n,b+n,a+n,color,color);
        }
        public void Ring(Vector2 center,float radius,float width,Color color,int segments=64,float rotation=0,float fraction=1)
        {
            Vector2 previous=center+new Vector2(Mathf.Cos(rotation),Mathf.Sin(rotation))*radius;
            for(int i=1;i<=segments;i++)
            {
                float a=rotation+i*Mathf.PI*2*fraction/segments;
                Vector2 next=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                Line(previous,next,width,color); previous=next;
            }
        }
        private void OnDestroy() { if(mesh!=null) Destroy(mesh); if(material!=null) Destroy(material); }
    }
}
