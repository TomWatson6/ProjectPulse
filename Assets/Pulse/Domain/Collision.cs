using System;

namespace Pulse.Domain
{
    public static class Collision
    {
        /// <summary>Swept AABB versus the actual spike triangle, using continuous separating axes.</summary>
        public static bool SweepSpike(double x0, double y0, double x1, double y1,
            double halfWidth, double halfHeight, Spike spike)
        {
            double enter = 0, exit = 1;
            double left = spike.X - spike.Width / 2, right = spike.X + spike.Width / 2;
            return Axis(1, 0, left, spike.Base, spike.X, spike.Base + spike.Height, right, spike.Base,
                       x0,y0,x1,y1,halfWidth,halfHeight,ref enter,ref exit)
                && Axis(0, 1, left, spike.Base, spike.X, spike.Base + spike.Height, right, spike.Base,
                       x0,y0,x1,y1,halfWidth,halfHeight,ref enter,ref exit)
                && Axis(spike.Height, -spike.Width / 2, left, spike.Base, spike.X, spike.Base + spike.Height, right, spike.Base,
                       x0,y0,x1,y1,halfWidth,halfHeight,ref enter,ref exit)
                && Axis(spike.Height, spike.Width / 2, left, spike.Base, spike.X, spike.Base + spike.Height, right, spike.Base,
                       x0,y0,x1,y1,halfWidth,halfHeight,ref enter,ref exit);
        }

        private static bool Axis(double ax,double ay,double lx,double ly,double tx,double ty,double rx,double ry,
            double x0,double y0,double x1,double y1,double hw,double hh,ref double enter,ref double exit)
        {
            double p0 = ax * lx + ay * ly, p1 = ax * tx + ay * ty, p2 = ax * rx + ay * ry;
            double radius = Math.Abs(ax) * hw + Math.Abs(ay) * hh;
            double min = Math.Min(p0, Math.Min(p1,p2)) - radius, max = Math.Max(p0, Math.Max(p1,p2)) + radius;
            double origin = ax*x0 + ay*y0, delta = ax*(x1-x0) + ay*(y1-y0);
            if (Math.Abs(delta) < 1e-12) return origin >= min && origin <= max;
            double a = (min-origin)/delta, b = (max-origin)/delta;
            enter = Math.Max(enter, Math.Min(a,b)); exit = Math.Min(exit, Math.Max(a,b));
            return enter <= exit;
        }
    }
}
