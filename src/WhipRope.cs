using System;
using System.Drawing;

namespace XWhip
{
    public sealed class WhipRope
    {
        private const int PointCount = 24;
        private const double StepSeconds = 1.0 / 120;
        private readonly PointF[] points = new PointF[PointCount];
        private readonly PointF[] previous = new PointF[PointCount];
        private bool initialized;
        private double remainder;
        private float currentLength;
        private PointF currentDirection;

        public PointF[] Points { get { return points; } }

        public void Reset(PointF anchor, PointF handleDirection, float length)
        {
            currentLength = ClampLength(length);
            currentDirection = Normalize(handleDirection);
            float segment = currentLength / (PointCount - 1);
            double angle = Math.Atan2(currentDirection.Y, currentDirection.X);
            double bend = -Math.PI / 2 - angle;
            while (bend > Math.PI) bend -= Math.PI * 2;
            while (bend < -Math.PI) bend += Math.PI * 2;
            points[0] = anchor;
            for (int i = 1; i < PointCount; i++)
            {
                double tangent = angle + bend * .65 * (i - 1) / (PointCount - 2);
                points[i] = new PointF(points[i - 1].X + (float)Math.Cos(tangent) * segment,
                    points[i - 1].Y + (float)Math.Sin(tangent) * segment);
            }
            Array.Copy(points, previous, PointCount);
            remainder = 0;
            initialized = true;
        }

        public void Advance(double elapsed, PointF anchor, PointF direction, float length, RectangleF bounds)
        {
            direction = Normalize(direction);
            length = ClampLength(length);
            if (!initialized || Distance(points[0], anchor) > 120) Reset(anchor, direction, length);
            PointF startAnchor = points[0];
            PointF startDirection = currentDirection;
            float startLength = currentLength;
            double duration = elapsed > 0 && !Double.IsNaN(elapsed) ? Math.Min(elapsed, .1) : 0;
            remainder += duration;
            int steps = (int)((remainder + 0.000000001) / StepSeconds);
            remainder = Math.Max(0, remainder - steps * StepSeconds);
            for (int step = 1; step <= steps; step++)
            {
                float fraction = (float)step / steps;
                PointF stepAnchor = Lerp(startAnchor, anchor, fraction);
                PointF stepDirection = Normalize(Lerp(startDirection, direction, fraction));
                float segment = (startLength + (length - startLength) * fraction) / (PointCount - 1);
                Pin(stepAnchor, stepDirection, segment);
                Integrate();
                for (int iteration = 0; iteration < 20; iteration++)
                {
                    for (int i = 2; i < PointCount; i++) Constrain(i, segment);
                    for (int i = PointCount - 1; i >= 2; i--) Constrain(i, segment);
                    for (int i = 2; i < PointCount; i++) Bounce(i, bounds);
                }
                // Bound cumulative stretch after the iterative solve, including fast anchor swings.
                for (int i = 2; i < PointCount; i++)
                {
                    float distance = Distance(points[i - 1], points[i]);
                    if (distance > segment * 1.02f)
                        points[i] = Lerp(points[i - 1], points[i], segment * 1.02f / distance);
                    Bounce(i, bounds);
                }
            }
            currentLength = length;
            currentDirection = direction;
            Pin(anchor, direction, length / (PointCount - 1));
        }

        private void Pin(PointF anchor, PointF direction, float segment)
        {
            points[0] = previous[0] = anchor;
            points[1] = previous[1] = new PointF(anchor.X + direction.X * segment,
                anchor.Y + direction.Y * segment);
        }

        private void Integrate()
        {
            for (int i = 2; i < PointCount; i++)
            {
                PointF point = points[i];
                points[i] = new PointF(point.X + (point.X - previous[i].X) * .994f,
                    point.Y + (point.Y - previous[i].Y) * .994f + (float)(480 * StepSeconds * StepSeconds));
                previous[i] = point;
            }
        }

        private void Constrain(int index, float segment)
        {
            PointF a = points[index - 1], b = points[index];
            float distance = Distance(a, b);
            if (distance < .0001f) return;
            float correction = (distance - segment) / distance;
            float x = (b.X - a.X) * correction, y = (b.Y - a.Y) * correction;
            if (index > 2)
            {
                x *= .5f; y *= .5f;
                points[index - 1] = new PointF(a.X + x, a.Y + y);
            }
            points[index] = new PointF(b.X - x, b.Y - y);
        }

        private void Bounce(int index, RectangleF bounds)
        {
            PointF point = points[index], old = previous[index];
            if (point.X < bounds.Left)
            {
                old.X = bounds.Left + Math.Min(0, point.X - old.X) * .4f;
                point.X = bounds.Left;
            }
            else if (point.X > bounds.Right)
            {
                old.X = bounds.Right + Math.Max(0, point.X - old.X) * .4f;
                point.X = bounds.Right;
            }
            if (point.Y < bounds.Top)
            {
                old.Y = bounds.Top + Math.Min(0, point.Y - old.Y) * .4f;
                point.Y = bounds.Top;
            }
            else if (point.Y > bounds.Bottom)
            {
                old.Y = bounds.Bottom + Math.Max(0, point.Y - old.Y) * .4f;
                point.Y = bounds.Bottom;
            }
            points[index] = point;
            previous[index] = old;
        }

        private static PointF Normalize(PointF direction)
        {
            float distance = Distance(PointF.Empty, direction);
            return distance > .0001f ? new PointF(direction.X / distance, direction.Y / distance) : new PointF(0, -1);
        }

        private static float ClampLength(float length) { return Math.Max(24, Math.Min(240, length)); }
        private static PointF Lerp(PointF a, PointF b, float fraction)
        { return new PointF(a.X + (b.X - a.X) * fraction, a.Y + (b.Y - a.Y) * fraction); }
        private static float Distance(PointF a, PointF b)
        { float x = b.X - a.X, y = b.Y - a.Y; return (float)Math.Sqrt(x * x + y * y); }
    }
}
