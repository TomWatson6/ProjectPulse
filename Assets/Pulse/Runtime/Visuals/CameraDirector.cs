using Pulse.Domain;
using UnityEngine;

namespace Pulse.Visuals
{
    public sealed class CameraDirector
    {
        private readonly Camera camera;
        public float CenterX => camera.transform.position.x;
        public float HalfWidth => camera.orthographicSize*camera.aspect;
        public float Height => camera.orthographicSize*2;
        public CameraDirector(Camera camera) { this.camera=camera; }
        public void Evaluate(float playerX,double beat,float energy,bool reducedMotion)
        {
            // Keep at least 18 world units ahead, including on 16:10 or narrow landscape screens.
            float baseSize=Mathf.Max(6.25f,11.1f/Mathf.Max(.5f,camera.aspect));
            float punch=beat>=48 && beat<50 ? Mathf.Exp(-(float)(beat-48)*3) : 0;
            float zoom=reducedMotion ? 0 : .25f*punch;
            camera.orthographicSize=baseSize+zoom;
            float halfWidth=camera.orthographicSize*camera.aspect;
            float shake=reducedMotion ? 0 : Mathf.Sin((float)beat*83)*punch*.06f;
            camera.transform.position=new Vector3(playerX+halfWidth*.56f,3.35f+shake,-10);
        }
    }
}
