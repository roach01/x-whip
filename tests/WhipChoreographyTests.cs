using System;
using System.Drawing;
using XWhip;

internal static class WhipChoreographyTests
{
    private const double Epsilon = .00001;
    private static int passed;

    private static int Main()
    {
        try
        {
            Run("Old .24-second cut remains continuous", OldCut);
            Run("All stage boundaries remain continuous", StageBoundaries);
            Run("Handle follows the hand at a fixed length", HandleGeometry);
            Run("Both arms maintain their segment lengths", ArmGeometry);
            Run("Non-jump feet stay planted except the opening stomp", PlantedFeet);
            Run("Jump variants lift both feet together", JumpFeet);
            Run("Photo rotation stays within twelve degrees", HeadAngleLimit);
            Run("Every variant returns to the standard resting pose", StandardRest);
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
    { float x = a.X - b.X, y = a.Y - b.Y; return (float)Math.Sqrt(x * x + y * y); }
    private static string Context(int variant, int level, double age)
    { return "variant=" + variant + " level=" + level + " age=" + age.ToString("F5"); }

    private static void Boundary(double age)
    {
        for (int variant = 0; variant < 8; variant++)
        for (int level = 1; level <= 6; level++)
        {
            WhipPose before = WhipChoreography.Sample(variant, level, age - Epsilon);
            WhipPose after = WhipChoreography.Sample(variant, level, age + Epsilon);
            string context = Context(variant, level, age);
            Check(Distance(before.Hand, after.Hand) < .2f, "hand discontinuity " + context);
            Check(Distance(before.Neck, after.Neck) < .2f, "neck discontinuity " + context);
            Check(Distance(before.HeadCenter, after.HeadCenter) < .2f, "head discontinuity " + context);
            Check(Distance(before.HandleTip, after.HandleTip) < .2f, "handle discontinuity " + context);
            Check(Math.Abs(before.HeadAngle - after.HeadAngle) < .1f, "photo-angle discontinuity " + context);
        }
    }

    private static void OldCut() { Boundary(.24); Boundary(.70); Boundary(.78); Boundary(1.08); }
    private static void StageBoundaries()
    { foreach (double boundary in new double[] { .30, .46, .62, 1.30 }) Boundary(boundary); }

    private static void ForEveryPose(Action<WhipPose, string> check)
    {
        for (int variant = 0; variant < 8; variant++)
        for (int level = 1; level <= 6; level++)
        for (int frame = 0; frame <= 320; frame++)
        {
            double age = frame / 200.0;
            check(WhipChoreography.Sample(variant, level, age), Context(variant, level, age));
        }
    }

    private static void HandleGeometry()
    {
        ForEveryPose(delegate(WhipPose pose, string context)
        {
            Near(1, Distance(PointF.Empty, pose.HandleDirection), .0001f, "unit direction " + context);
            PointF tip = new PointF(pose.Hand.X + pose.HandleDirection.X * 20,
                pose.Hand.Y + pose.HandleDirection.Y * 20);
            Near(0, Distance(tip, pose.HandleTip), .001f, "handle tip " + context);
            Near(20, Distance(pose.Hand, pose.HandleTip), .001f, "handle length " + context);
        });
    }

    private static void ArmGeometry()
    {
        ForEveryPose(delegate(WhipPose pose, string context)
        {
            Near(48, Distance(pose.Shoulder, pose.Elbow), .01f, "whip upper arm " + context);
            Near(46, Distance(pose.Elbow, pose.Hand), .01f, "whip forearm " + context);
            PointF freeShoulder = new PointF(pose.Shoulder.X - 5, pose.Shoulder.Y);
            Near(48, Distance(freeShoulder, pose.FreeElbow), .01f, "free upper arm " + context);
            Near(46, Distance(pose.FreeElbow, pose.FreeHand), .01f, "free forearm " + context);
        });
    }

    private static void PlantedFeet()
    {
        for (int variant = 0; variant <= 4; variant++)
        for (int level = 1; level <= 6; level++)
        for (int frame = 0; frame <= 320; frame++)
        {
            double age = frame / 200.0;
            WhipPose pose = WhipChoreography.Sample(variant, level, age);
            string context = Context(variant, level, age);
            Near(130, pose.LeftFoot.X, .001f, "planted left x " + context);
            Near(183, pose.RightFoot.X, .001f, "planted right x " + context);
            Near(250, pose.RightFoot.Y, .001f, "planted right y " + context);
            if (variant != 3 || age >= .30 || age == 0)
                Near(250, pose.LeftFoot.Y, .001f, "planted left y " + context);
            Near(0, pose.Jump, 0, "non-jump displacement " + context);
        }
        Check(WhipChoreography.Sample(3, 1, .15).LeftFoot.Y < 240, "opening stomp lifts one foot");
        Near(250, WhipChoreography.Sample(3, 1, .30).LeftFoot.Y, .001f, "stomp finishes at .30 seconds");
    }

    private static void JumpFeet()
    {
        for (int variant = 5; variant < 8; variant++)
        for (int level = 1; level <= 6; level++)
        {
            WhipPose pose = WhipChoreography.Sample(variant, level, .78);
            Check(pose.Jump > 0, "jump variant should lift " + Context(variant, level, .78));
            Near(250 - pose.Jump, pose.LeftFoot.Y, .001f, "jumping left foot");
            Near(250 - pose.Jump, pose.RightFoot.Y, .001f, "jumping right foot");
        }
    }

    private static void StandardRest()
    {
        WhipPose expected = WhipChoreography.Sample(0, 1, 0);
        string[] names = { "hip", "neck", "shoulder", "hand", "elbow", "free hand", "free elbow", "left foot", "right foot", "head", "handle tip", "handle direction" };
        PointF[] standard = Points(expected);
        for (int variant = 0; variant < 8; variant++)
        for (int level = 1; level <= 6; level++)
        foreach (double age in new double[] { 1.30, 1.30 + Epsilon, 2, 15 })
        {
            WhipPose pose = WhipChoreography.Sample(variant, level, age);
            PointF[] actual = Points(pose);
            string context = Context(variant, level, age);
            for (int i = 0; i < standard.Length; i++)
                Near(0, Distance(standard[i], actual[i]), .01f, "standard " + names[i] + " " + context);
            Near(0, pose.HeadAngle, .001f, "resting head angle " + context);
            Near(0, pose.Reaction, .001f, "resting reaction " + context);
            Near(0, pose.Wind, .001f, "resting windup " + context);
            Near(0, pose.Snap, .001f, "resting snap " + context);
            Near(0, pose.Jump, .001f, "resting jump " + context);
        }
    }

    private static PointF[] Points(WhipPose pose)
    {
        return new PointF[] { pose.Hip, pose.Neck, pose.Shoulder, pose.Hand, pose.Elbow,
            pose.FreeHand, pose.FreeElbow, pose.LeftFoot, pose.RightFoot, pose.HeadCenter,
            pose.HandleTip, pose.HandleDirection };
    }

    private static void HeadAngleLimit()
    {
        ForEveryPose(delegate(WhipPose pose, string context)
        { Check(Math.Abs(pose.HeadAngle) <= 12, "excess photo rotation " + context); });
    }
}
