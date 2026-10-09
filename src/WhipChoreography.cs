using System;
using System.Drawing;

namespace XWhip
{
    public struct WhipPose
    {
        public PointF Hip, Neck, Shoulder, Hand, Elbow, FreeHand, FreeElbow, LeftFoot, RightFoot, HeadCenter, HandleTip, HandleDirection;
        public float HeadAngle, Reaction, Wind, Snap, Jump;
    }

    public static class WhipChoreography
    {
        public const double ImpactSeconds = .64;
        public const double Duration = 1.3;

        public static WhipPose Sample(int variant, int level, double age)
        {
            variant = Math.Max(0, variant) % 8;
            level = Math.Max(1, Math.Min(6, level));
            age = Math.Max(0, age);
            float wind = Ease(age / .30), strike = Ease((age - .30) / .16);
            float recover = Ease((age - .62) / .68);
            float reaction = Ease((age - .70) / .08) * (1 - Ease((age - 1.08) / .22));
            float lean = (-13 * wind + 31 * strike) * (1 - Ease((age - .46) / .40));
            float recoil = variant >= 4 ? reaction * -14 : 0;
            float jump = (variant == 5 ? 26 : variant == 6 ? 48 : variant == 7 ? 18 : 0) * reaction;
            float crouch = (variant == 2 ? 7 : 2) * wind * (1 - strike);
            WhipPose pose = new WhipPose();
            pose.Wind = wind * (1 - strike); pose.Snap = strike * (1 - recover);
            pose.Reaction = reaction; pose.Jump = jump;
            pose.Hip = new PointF(156 + recoil, 217 + crouch - jump);
            pose.Neck = new PointF(pose.Hip.X + lean, 172 + crouch - jump);
            pose.Shoulder = new PointF(pose.Neck.X, pose.Neck.Y + 12);
            pose.HeadCenter = new PointF(pose.Neck.X + 2, pose.Neck.Y - 64);
            pose.HeadAngle = -pose.Wind * 5 + pose.Snap * 3 + (variant >= 4 ? reaction * -9 : 0);
            pose.LeftFoot = new PointF(130, 250 - jump);
            pose.RightFoot = new PointF(183, 250 - jump);
            if (variant == 6 || variant == 7)
            {
                float half = variant == 6 ? 10 : 8;
                pose.LeftFoot.X = Mix(130, pose.Hip.X - half, reaction);
                pose.RightFoot.X = Mix(183, pose.Hip.X + half, reaction);
            }
            if (variant == 3) pose.LeftFoot.Y -= (float)Math.Sin(Math.Min(1, age / .30) * Math.PI) * 17;
            PointF rest = new PointF(205, 181);
            PointF[] backs = { new PointF(89, 133), new PointF(113, 139), new PointF(140, 93), new PointF(115, 160), new PointF(94, 135), new PointF(129, 103), new PointF(105, 130), new PointF(97, 152) };
            PointF[] fronts = { new PointF(248, 187), new PointF(252, 178), new PointF(220, 207), new PointF(227, 181), new PointF(252, 190), new PointF(225, 169), new PointF(250, 159), new PointF(245, 205) };
            PointF target;
            float handleAngle;
            if (age < .30)
            { target = Lerp(rest, backs[variant], wind); handleAngle = Mix(-1.2f, -2.0f, wind); }
            else if (age < .46)
            { target = Lerp(backs[variant], fronts[variant], strike); handleAngle = Mix(-2.0f, .15f, strike); }
            else if (age < .62)
            { float t = Ease((age - .46) / .16); target = Lerp(fronts[variant], new PointF(206, 192), t); handleAngle = Mix(.15f, -.8f, t); }
            else
            { target = Lerp(new PointF(206, 192), rest, recover); handleAngle = Mix(-.8f, -1.2f, recover); }
            target.X += recoil; target.Y -= jump;
            if (variant == 6) { target.X += reaction * 30; target.Y -= reaction * 25; }
            Arm(pose.Shoulder, target, -1, out pose.Hand, out pose.Elbow);
            PointF other = variant == 2 ? new PointF(pose.Hand.X - 7, pose.Hand.Y + 8) :
                variant >= 4 ? new PointF(116 + recoil, 195 - jump - reaction * 20) : new PointF(145, 210);
            other = Lerp(new PointF(145, 210), other, wind * (1 - recover));
            Arm(new PointF(pose.Shoulder.X - 5, pose.Shoulder.Y), other, 1, out pose.FreeHand, out pose.FreeElbow);
            pose.HandleDirection = new PointF((float)Math.Cos(handleAngle), (float)Math.Sin(handleAngle));
            pose.HandleTip = new PointF(pose.Hand.X + pose.HandleDirection.X * 20, pose.Hand.Y + pose.HandleDirection.Y * 20);
            return pose;
        }

        static void Arm(PointF shoulder, PointF target, int side, out PointF hand, out PointF elbow)
        {
            const float upper = 48, lower = 46;
            float x = target.X - shoulder.X, y = target.Y - shoulder.Y;
            float distance = Math.Max(.01f, (float)Math.Sqrt(x * x + y * y));
            float ux = x / distance, uy = y / distance;
            distance = Math.Max(2.1f, Math.Min(93, distance));
            hand = new PointF(shoulder.X + ux * distance, shoulder.Y + uy * distance);
            float along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
            float height = (float)Math.Sqrt(Math.Max(0, upper * upper - along * along));
            elbow = new PointF(shoulder.X + ux * along - uy * height * side, shoulder.Y + uy * along + ux * height * side);
        }
        static float Ease(double value) { float t = (float)Math.Max(0, Math.Min(1, value)); return t * t * (3 - 2 * t); }
        static float Mix(float a, float b, float t) { return a + (b - a) * t; }
        static PointF Lerp(PointF a, PointF b, float t) { return new PointF(Mix(a.X, b.X, t), Mix(a.Y, b.Y, t)); }
    }
}
