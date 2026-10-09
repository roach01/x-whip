using System;
using System.Drawing;
using XWhip;

internal static class WhipRopeTests
{
    private static readonly RectangleF Room = new RectangleF(-400, -400, 1600, 1400);
    private static int passed;

    private static int Main()
    {
        try
        {
            Run("Reset pins root and initializes a continuous arc", ResetArc);
            Run("Advance initializes a new rope", FirstAdvance);
            Run("Zero elapsed pins only the first two points", ZeroElapsed);
            Run("Moving and rotating root stays pinned", PinnedRoot);
            Run("Fast anchor swing leaves a trailing tip", FastAnchor);
            Run("Released tip keeps moving and reverses", Rebound);
            Run("Large anchor teleport resets without a stretched chain", Teleport);
            Run("Length remains bounded at every segment", BoundedLength);
            Run("Thirty and one hundred twenty FPS converge", FrameRates);
            Run("Long elapsed time advances only the capped duration", LongElapsed);
            Run("Wall and floor collisions remain inside bounds", Boundaries);
            Run("Repeated fast swings remain finite and connected", Stress);
            Console.WriteLine("PASS: " + passed + " tests");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            return 1;
        }
    }

    private static void Run(string name, Action test)
    { test(); passed++; Console.WriteLine("PASS " + name); }
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); }
    private static void Near(float expected, float actual, float tolerance, string message)
    { Check(Math.Abs(expected - actual) <= tolerance, message + ": expected " + expected + ", got " + actual); }
    private static float Distance(PointF a, PointF b)
    { float x = b.X - a.X, y = b.Y - a.Y; return (float)Math.Sqrt(x * x + y * y); }
    private static void Pinned(WhipRope rope, PointF anchor, PointF direction, float length)
    {
        Near(0, Distance(rope.Points[0], anchor), .001f, "anchor pin");
        float magnitude = Distance(PointF.Empty, direction);
        PointF expected = new PointF(anchor.X + direction.X / magnitude * length / 23,
            anchor.Y + direction.Y / magnitude * length / 23);
        Near(0, Distance(rope.Points[1], expected), .001f, "direction pin");
    }
    private static void Connected(WhipRope rope, float length)
    {
        float total = 0;
        for (int i = 0; i < rope.Points.Length; i++)
        {
            PointF point = rope.Points[i];
            Check(!Single.IsNaN(point.X) && !Single.IsInfinity(point.X) &&
                !Single.IsNaN(point.Y) && !Single.IsInfinity(point.Y), "finite point " + i);
            if (i == 0) continue;
            float segment = Distance(rope.Points[i - 1], point);
            Check(segment <= length / 23 * 1.021f + .001f, "segment stretch " + i + ": " + segment);
            total += segment;
        }
        Check(total <= length * 1.021f, "total stretch: " + total);
    }

    private static void ResetArc()
    {
        var rope = new WhipRope();
        PointF anchor = new PointF(200, 220), direction = new PointF(1, -.25f);
        rope.Reset(anchor, direction, 210);
        Check(rope.Points.Length == 24, "twenty-three segments");
        Pinned(rope, anchor, direction, 210);
        for (int i = 1; i < rope.Points.Length; i++)
            Near(210f / 23, Distance(rope.Points[i - 1], rope.Points[i]), .001f, "initial segment " + i);
        Check(rope.Points[23].Y < anchor.Y, "initial chain curves upward");
    }

    private static void FirstAdvance()
    {
        var rope = new WhipRope();
        PointF anchor = new PointF(220, 200), direction = new PointF(0, -1);
        rope.Advance(1.0 / 60, anchor, direction, 180, Room);
        Pinned(rope, anchor, direction, 180);
        Connected(rope, 180);
    }

    private static void ZeroElapsed()
    {
        var rope = new WhipRope();
        rope.Reset(new PointF(200, 200), new PointF(0, -1), 180);
        PointF[] before = (PointF[])rope.Points.Clone();
        PointF anchor = new PointF(230, 200), direction = new PointF(1, 0);
        rope.Advance(0, anchor, direction, 180, Room);
        Pinned(rope, anchor, direction, 180);
        for (int i = 2; i < before.Length; i++)
            Near(0, Distance(before[i], rope.Points[i]), 0, "zero elapsed free point " + i);
        rope.Advance(-1, anchor, direction, 180, Room);
        for (int i = 2; i < before.Length; i++)
            Near(0, Distance(before[i], rope.Points[i]), 0, "negative elapsed free point " + i);
    }

    private static void PinnedRoot()
    {
        var rope = new WhipRope();
        for (int i = 0; i < 240; i++)
        {
            float angle = i * .035f;
            PointF anchor = new PointF(230 + 35 * (float)Math.Sin(angle), 200);
            PointF direction = new PointF((float)Math.Sin(angle), -(float)Math.Cos(angle));
            rope.Advance(1.0 / 60, anchor, direction, 200, Room);
            Pinned(rope, anchor, direction, 200);
            Connected(rope, 200);
        }
    }

    private static WhipRope Swing()
    {
        var rope = new WhipRope();
        rope.Reset(new PointF(200, 220), new PointF(0, -1), 200);
        rope.Advance(1.0 / 60, new PointF(280, 220), new PointF(0, -1), 200, Room);
        return rope;
    }

    private static void FastAnchor()
    {
        var rope = Swing();
        Pinned(rope, new PointF(280, 220), new PointF(0, -1), 200);
        Check(rope.Points[23].X < 260, "tip should lag an eighty-pixel anchor swing");
        Connected(rope, 200);
    }

    private static void Rebound()
    {
        var rope = Swing();
        float initialX = rope.Points[23].X, lastX = initialX;
        bool movedRight = false, reversed = false;
        for (int i = 0; i < 240; i++)
        {
            rope.Advance(1.0 / 120, new PointF(280, 220), new PointF(0, -1), 200, Room);
            float x = rope.Points[23].X;
            if (x - lastX > .03f) movedRight = true;
            if (movedRight && x - lastX < -.03f) reversed = true;
            lastX = x;
            Connected(rope, 200);
        }
        Check(movedRight, "tip continues moving after the root stops");
        Check(reversed, "tip rebounds instead of following a rigid root translation");
        Check(Math.Abs(lastX - initialX) > 1, "physical motion is retained between frames");
    }

    private static void Teleport()
    {
        var moved = Swing();
        PointF anchor = new PointF(500, 260), direction = new PointF(.7f, -.7f);
        moved.Advance(0, anchor, direction, 220, Room);
        var fresh = new WhipRope();
        fresh.Reset(anchor, direction, 220);
        for (int i = 0; i < moved.Points.Length; i++)
            Near(0, Distance(fresh.Points[i], moved.Points[i]), .001f, "teleport resets point " + i);
    }

    private static void BoundedLength()
    {
        var rope = new WhipRope();
        PointF anchor = new PointF(250, 250), direction = new PointF(0, -1);
        rope.Reset(anchor, direction, 1000);
        Connected(rope, 240);
        for (int i = 0; i < 120; i++) rope.Advance(1.0 / 60, anchor, direction, 1000, Room);
        Connected(rope, 240);
        rope.Reset(anchor, direction, -100);
        Connected(rope, 24);
        rope.Advance(1.0 / 60, anchor, PointF.Empty, 180, Room);
        Pinned(rope, anchor, direction, 180);
        Connected(rope, 180);
    }

    private static WhipRope AtFrameRate(int fps)
    {
        var rope = new WhipRope();
        rope.Reset(new PointF(220, 255), new PointF(0, -1), 200);
        for (int i = 1; i <= fps * 4; i++)
        {
            double time = (double)i / fps;
            PointF anchor = new PointF(220 + 45 * (float)Math.Sin(time * 4), 240 + 15 * (float)Math.Cos(time));
            PointF direction = new PointF(.4f * (float)Math.Sin(time * 6), -1);
            rope.Advance(1.0 / fps, anchor, direction, 200, Room);
        }
        return rope;
    }

    private static void FrameRates()
    {
        WhipRope slow = AtFrameRate(30), fast = AtFrameRate(120);
        float sum = 0;
        for (int i = 0; i < slow.Points.Length; i++) sum += Distance(slow.Points[i], fast.Points[i]);
        Check(sum / slow.Points.Length < 5, "average frame-rate difference: " + sum / slow.Points.Length);
        Connected(slow, 200); Connected(fast, 200);
    }

    private static void LongElapsed()
    {
        var longFrame = new WhipRope(); var cappedFrame = new WhipRope();
        PointF anchor = new PointF(250, 220), direction = new PointF(0, -1);
        longFrame.Reset(anchor, direction, 200); cappedFrame.Reset(anchor, direction, 200);
        longFrame.Advance(3600, anchor, direction, 200, Room);
        cappedFrame.Advance(.1, anchor, direction, 200, Room);
        for (int i = 0; i < longFrame.Points.Length; i++)
            Near(0, Distance(longFrame.Points[i], cappedFrame.Points[i]), .001f, "elapsed cap point " + i);
        Connected(longFrame, 200);
    }

    private static void Boundaries()
    {
        var rope = new WhipRope();
        RectangleF bounds = new RectangleF(0, 0, 300, 180);
        rope.Reset(new PointF(75, 85), new PointF(1, 0), 200);
        for (int i = 0; i < 240; i++)
        {
            PointF anchor = new PointF(75 + 100 * (float)Math.Sin(i * .04), 85);
            anchor.X = Math.Max(20, anchor.X);
            rope.Advance(1.0 / 60, anchor, new PointF(1, 0), 200, bounds);
            for (int p = 2; p < rope.Points.Length; p++)
            {
                PointF point = rope.Points[p];
                Check(point.X >= bounds.Left && point.X <= bounds.Right &&
                    point.Y >= bounds.Top && point.Y <= bounds.Bottom, "boundary point " + p);
            }
            Connected(rope, 200);
        }
    }

    private static void Stress()
    {
        var rope = new WhipRope();
        RectangleF bounds = new RectangleF(0, 0, 640, 480);
        for (int i = 0; i < 1000; i++)
        {
            PointF anchor = new PointF(320 + 80 * (float)Math.Cos(i * .35), 200 + 60 * (float)Math.Sin(i * .35));
            PointF direction = new PointF((float)Math.Sin(i * .3), -(float)Math.Cos(i * .3));
            float length = 120 + i % 6 * 20;
            rope.Advance(i % 9 == 0 ? 2 : 1.0 / 60, anchor, direction, length, bounds);
            Pinned(rope, anchor, direction, length);
            Connected(rope, length);
        }
    }
}
