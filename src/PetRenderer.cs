using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;

namespace XWhip
{
    internal sealed class PetRenderer : IDisposable
    {
        readonly Bitmap faces;
        readonly Rectangle[] faceRects;
        readonly WhipRope rope = new WhipRope();
        PointF[] lastRope, olderRope;
        bool wasWhipping;
        double ropeTime;
        double lastRenderTime = Double.NaN, frozenWalkPhase;
        float walkBlend;
        internal static readonly Color Ink = Color.FromArgb(29, 29, 27);
        internal static readonly Color Orange = Color.FromArgb(241, 101, 53);
        internal PetRenderer()
        {
            using (System.IO.Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Faces"))
            using (Bitmap source = new Bitmap(stream)) faces = new Bitmap(source);
            int cellWidth = faces.Width / 4, cellHeight = faces.Height / 4;
            faceRects = new Rectangle[16]; int cropWidth = 0, cropHeight = 0;
            for (int i = 0; i < 16; i++)
            {
                Rectangle region = new Rectangle(i % 4 * cellWidth, i / 4 * cellHeight, cellWidth, cellHeight);
                faceRects[i] = VisibleBounds(region);
                cropWidth = Math.Max(cropWidth, faceRects[i].Width); cropHeight = Math.Max(cropHeight, faceRects[i].Height);
            }
            cropWidth = Math.Min(cellWidth, cropWidth + 4); cropHeight = Math.Min(cellHeight, cropHeight + 4);
            for (int i = 0; i < 16; i++)
            {
                Rectangle bounds = faceRects[i]; int left = i % 4 * cellWidth, top = i / 4 * cellHeight;
                int x = Math.Max(left, Math.Min(left + cellWidth - cropWidth, bounds.Left + bounds.Width / 2 - cropWidth / 2));
                int y = Math.Max(top, Math.Min(top + cellHeight - cropHeight, bounds.Top - 2));
                faceRects[i] = new Rectangle(x, y, cropWidth, cropHeight);
            }
        }

        Rectangle VisibleBounds(Rectangle region)
        {
            int width = region.Width, height = region.Height, largest = 0;
            byte[] mask = new byte[width * height]; int[] queue = new int[mask.Length];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (faces.GetPixel(region.X + x, region.Y + y).A > 64) mask[y * width + x] = 1;
            Rectangle result = region;
            for (int start = 0; start < mask.Length; start++)
            {
                if (mask[start] != 1) continue;
                int read = 0, count = 1, left = width, top = height, right = 0, bottom = 0;
                queue[0] = start; mask[start] = 2;
                while (read < count)
                {
                    int at = queue[read++], x = at % width, y = at / width;
                    left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                            int next = ny * width + nx;
                            if (mask[next] == 1) { mask[next] = 2; queue[count++] = next; }
                        }
                }
                if (count > largest) { largest = count; result = Rectangle.FromLTRB(region.X + left, region.Y + top, region.X + right + 1, region.Y + bottom + 1); }
            }
            return result;
        }

        internal Icon CreateIcon()
        {
            using (Bitmap bitmap = new Bitmap(64, 64, PixelFormat.Format32bppPArgb))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                Rectangle rect = faceRects[0]; float width = 60f * rect.Width / rect.Height;
                g.DrawImage(faces, new RectangleF(32 - width / 2, 2, width, 60), rect, GraphicsUnit.Pixel);
                IntPtr handle = bitmap.GetHicon();
                try { using (Icon icon = Icon.FromHandle(handle)) return (Icon)icon.Clone(); }
                finally { Native.DestroyIcon(handle); }
            }
        }

        internal static Size CanvasSize(PetMode mode, int size)
        {
            float scale = size / 180f;
            return new Size((int)((mode == PetMode.Whipping ? 640 : 320) * scale), (int)((mode == PetMode.Whipping ? 420 : 280) * scale));
        }
        internal static Point ActorOffset(PetMode mode, int size)
        {
            float scale = size / 180f;
            return mode == PetMode.Whipping ? new Point((int)(164 * scale), (int)(140 * scale)) : Point.Empty;
        }

        internal Bitmap Render(PetState state, DateTime now, double time, double strikeAge, string message, bool celebration, int size, bool walking = false, int facing = 1, double walkPhase = 0, int dailyCount = 0)
        {
            float scale = size / 180f;
            Size canvas = CanvasSize(state.Mode, size);
            Bitmap frame = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(frame))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.ScaleTransform(scale, scale);
                if (state.Mode == PetMode.Whipping)
                {
                    walkBlend = 0;
                    RenderWhipping(g, state, time, strikeAge, message, dailyCount);
                    return frame;
                }
                wasWhipping = false; lastRope = olderRope = null;
                bool mayWalk = state.Mode == PetMode.Cooling;
                double elapsed = Double.IsNaN(lastRenderTime) || time < lastRenderTime ? 0 : Math.Min(.15, time - lastRenderTime);
                if (Double.IsNaN(lastRenderTime) || time < lastRenderTime) { walkBlend = 0; frozenWalkPhase = walkPhase; }
                lastRenderTime = time;
                if (!mayWalk) walkBlend = 0;
                else walkBlend = Math.Max(0, Math.Min(1, walkBlend + (walking ? 1 : -1) * (float)(elapsed / .15)));
                if (walking && mayWalk) frozenWalkPhase = walkPhase;
                walkPhase = frozenWalkPhase;
                walking = mayWalk && walkBlend > 0;
                int stage = state.CountdownStage(now);
                float bounce = walking ? -(float)Math.Abs(Math.Sin(walkPhase * 2)) * 1.5f * walkBlend : 0;
                if (celebration) bounce -= (float)(Math.Abs(Math.Sin(time * 5)) * 18);
                using (Brush shadow = new SolidBrush(Color.FromArgb(35, 0, 0, 0))) g.FillEllipse(shadow, 124, 252, 70, 8);
                g.TranslateTransform(0, bounce);

                Color mood = stage >= 2 ? Orange : Ink;
                GraphicsState actor = g.Save();

                using (Pen pen = new Pen(Ink, 4.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    g.DrawLine(pen, 156, 170, 156, 217);
                    float gait = walking ? (float)Math.Sin(walkPhase) * walkBlend : 0;
                    DrawLeg(g, pen, 143 + gait * 8, 250 - (walking ? (float)Math.Max(0, Math.Sin(walkPhase)) * 9 * walkBlend + bounce : 0), facing, walking);
                    DrawLeg(g, pen, 172 - gait * 8, 250 - (walking ? (float)Math.Max(0, -Math.Sin(walkPhase)) * 9 * walkBlend + bounce : 0), facing, walking);
                    if (walking)
                    {
                        g.DrawLines(pen, new PointF[] { new PointF(153, 184), new PointF(139 - gait * 5, 201), new PointF(134 - gait * 9, 214) });
                        g.DrawLines(pen, new PointF[] { new PointF(160, 184), new PointF(175 + gait * 5, 201), new PointF(182 + gait * 9, 214) });
                    }
                    else if (stage == 3)
                    {
                        g.DrawLines(pen, new PointF[] { new PointF(154, 186), new PointF(135, 199), new PointF(143, 208) });
                        float handX = 200, handY = 140;
                        g.DrawLines(pen, new PointF[] { new PointF(160, 187), new PointF(182, 170), new PointF(handX, handY) });
                        using (Pen handle = new Pen(Color.FromArgb(137, 71, 43), 6) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                            g.DrawLine(handle, handX - 3, handY + 4, handX + 7, handY - 11);
                        using (Pen rope = new Pen(Ink, 2.5f))
                        {
                            g.DrawBezier(rope, handX + 7, handY - 11, 240, 109, 265, 161, 234, 198);
                        }
                    }
                    else if (stage == 1)
                    {
                        g.DrawLines(pen, new PointF[] { new PointF(153, 184), new PointF(135, 199), new PointF(172, 202) });
                        g.DrawLines(pen, new PointF[] { new PointF(160, 184), new PointF(178, 197), new PointF(140, 205) });
                    }
                    else if (stage == 2)
                    {
                        g.DrawLines(pen, new PointF[] { new PointF(153, 184), new PointF(135, 199), new PointF(146, 207) });
                        g.DrawLines(pen, new PointF[] { new PointF(160, 184), new PointF(184, 193), new PointF(204, 181) });
                    }
                    else
                    {
                        g.DrawLines(pen, new PointF[] { new PointF(153, 184), new PointF(135, 201), new PointF(131, 217) });
                        g.DrawLines(pen, new PointF[] { new PointF(160, 184), new PointF(178, 201), new PointF(183, 215) });
                    }
                }

                int[] relaxed = { 0, 1, 2, 8, 10, 11, 9 };
                int expression = stage == 0 ? relaxed[(int)(time / 12) % relaxed.Length] : stage == 1 ? ((int)(time / 10) % 2 == 0 ? 3 : 9) : stage == 2 ? ((int)(time / 10) % 3 == 2 ? 13 : 4 + (int)(time / 10) % 2) : ((int)(time / 10) % 2 == 0 ? 6 : 12);
                if (!String.IsNullOrEmpty(message) && (celebration || message.Contains("收手") || message.Contains("放你"))) expression = 0;
                Rectangle cell = faceRects[expression];
                float lean = facing * 3;
                float headHeight = 128;
                float headWidth = headHeight * cell.Width / cell.Height;
                g.DrawImage(faces, new RectangleF(158 - headWidth / 2 + lean, 176 - headHeight, headWidth, headHeight), cell, GraphicsUnit.Pixel);
                if (stage >= 2 && String.IsNullOrEmpty(message))
                {
                    using (Pen steam = new Pen(Orange, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        float jitter = (float)Math.Sin(time * 8) * 3;
                        g.DrawBezier(steam, 223, 103, 234, 96, 219, 87, 229, 79 + jitter);
                        if (stage >= 3) g.DrawBezier(steam, 234, 113, 245, 105, 231, 95, 239, 87 - jitter);
                    }
                }
                PointF[] headCorners = { new PointF(158 - headWidth / 2 + lean, 48), new PointF(158 + headWidth / 2 + lean, 48), new PointF(158 - headWidth / 2 + lean, 176), new PointF(158 + headWidth / 2 + lean, 176) };
                using (Matrix transform = g.Transform) transform.TransformPoints(headCorners);
                g.Restore(actor);
                using (Matrix transform = g.Transform) { transform.Invert(); transform.TransformPoints(headCorners); }
                DrawBubble(g, message, HeadBounds(headCorners), new RectangleF(0, -bounce, 320, 280));

                TimeSpan remaining = state.Remaining(now);
                string[] names = { "摸鱼", "盯你", "催促", "蓄力" };
                string status = names[Math.Min(stage, 3)] + "  ·  " + ((int)remaining.TotalMinutes).ToString("00") + ":" + remaining.Seconds.ToString("00");
                DrawFooter(g, status + "  ·  今日记录 " + dailyCount, mood, 263);
            }
            return frame;
        }

        static void DrawLeg(Graphics g, Pen pen, float footX, float footY, int facing, bool walking)
        {
            float lift = Math.Max(0, 250 - footY);
            g.DrawLines(pen, new PointF[] { new PointF(156, 217), new PointF((156 + footX) / 2 + (walking ? lift * .65f : 0), footY - 14), new PointF(footX, footY) });
            g.DrawLine(pen, footX - facing * 2, footY, footX + facing * 9, footY - lift * .18f);
        }

        static RectangleF HeadBounds(PointF[] points)
        {
            float left = points[0].X, right = left, top = points[0].Y, bottom = top;
            foreach (PointF p in points) { left = Math.Min(left, p.X); right = Math.Max(right, p.X); top = Math.Min(top, p.Y); bottom = Math.Max(bottom, p.Y); }
            return RectangleF.FromLTRB(left, top, right, bottom);
        }

        static void DrawBubble(Graphics g, string message, RectangleF head, RectangleF canvas)
        {
            if (String.IsNullOrEmpty(message)) return;
            float x = Math.Max(canvas.Left + 4, Math.Min(canvas.Right - 244, head.Left + head.Width / 2 - 120));
            float y = Math.Max(canvas.Top + 4, head.Top - 39);
            using (GraphicsPath bubble = Rounded(new RectangleF(x, y, 240, 34), 11))
            using (Brush paper = new SolidBrush(Color.FromArgb(248, 246, 242, 233)))
            using (Pen edge = new Pen(Color.FromArgb(45, Ink), 1)) { g.FillPath(paper, bubble); g.DrawPath(edge, bubble); }
            using (Font font = new Font("Microsoft YaHei", 9, FontStyle.Bold))
            using (Brush ink = new SolidBrush(Ink))
            using (StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(message, font, ink, new RectangleF(x + 6, y + 2, 228, 30), center);
        }

        static void DrawFooter(Graphics g, string status, Color color, float y)
        {
            using (GraphicsPath pill = Rounded(new RectangleF(38, y, 244, 17), 8))
            using (Brush paper = new SolidBrush(Color.FromArgb(245, 246, 242, 233))) g.FillPath(paper, pill);
            using (Font font = new Font("Microsoft YaHei", 7.6f, FontStyle.Bold))
            using (Brush ink = new SolidBrush(color))
            using (StringFormat center = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(status, font, ink, new RectangleF(38, y - 1, 244, 18), center);
        }

        void RenderWhipping(Graphics g, PetState state, double time, double age, string message, int dailyCount)
        {
            age = Math.Max(0, age);
            int variant = Math.Max(0, state.StrikeCount - 1) % 8;
            WhipPose pose = WhipChoreography.Sample(variant, state.AngerLevel, age);
            float length = 145 + (state.AngerLevel - 1) * 17;
            if (!wasWhipping || time <= ropeTime)
            {
                rope.Reset(pose.HandleTip, pose.HandleDirection, length);
                lastRope = olderRope = null;
            }
            rope.Advance(wasWhipping ? Math.Max(0, time - ropeTime) : 0,
                pose.HandleTip, pose.HandleDirection, length, new RectangleF(-154, -132, 620, 388));
            wasWhipping = true; ropeTime = time;
            PointF[] shape = ComedyRope(rope.Points, pose, variant, age);
            g.TranslateTransform(164, 140);
            if (variant == 6)
            {
                using (Pen post = new Pen(Ink, 3)) { g.DrawLine(post, 385, -90, 385, -42); g.DrawLine(post, 371, -90, 397, -90); }
                using (Brush sign = new SolidBrush(Ink)) g.FillRectangle(sign, 369, -42, 33, 36);
                using (Font font = new Font("Consolas", 19, FontStyle.Bold))
                using (Brush white = new SolidBrush(Color.White)) g.DrawString("X", font, white, 375, -39);
            }
            using (Brush shadow = new SolidBrush(Color.FromArgb(35, 0, 0, 0)))
                g.FillEllipse(shadow, 122, 252, 74, 8);
            if (age >= .30 && age <= .70)
            {
                if (olderRope != null) DrawRope(g, olderRope, Color.FromArgb(28, Orange), false);
                if (lastRope != null) DrawRope(g, lastRope, Color.FromArgb(65, Orange), false);
            }
            DrawRope(g, shape, Color.FromArgb(79, 47, 29), true);
            using (Pen pen = new Pen(Ink, 4.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawLine(pen, pose.Neck, pose.Hip);
                DrawPoseLeg(g, pen, pose.Hip, pose.LeftFoot, -1, variant == 6 ? pose.Reaction : 0);
                DrawPoseLeg(g, pen, pose.Hip, pose.RightFoot, 1, variant == 6 ? pose.Reaction : 0);
                g.DrawLines(pen, new PointF[] { new PointF(pose.Shoulder.X - 5, pose.Shoulder.Y), pose.FreeElbow, pose.FreeHand });
                g.DrawLines(pen, new PointF[] { pose.Shoulder, pose.Elbow, pose.Hand });
            }
            using (Pen handle = new Pen(Color.FromArgb(137, 71, 43), 6) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(handle, new PointF(pose.Hand.X - pose.HandleDirection.X * 5, pose.Hand.Y - pose.HandleDirection.Y * 5), pose.HandleTip);
            int[] furious = { 5, 6, 7, 13, 14, 15 };
            int expression = furious[Math.Min(5, state.AngerLevel - 1)];
            if (variant >= 4 && age >= .78 && age < 1.16) expression = 9;
            if (variant == 4 && age >= 1.16 && age < 1.82) expression = 2;
            Rectangle cell = faceRects[expression];
            float headWidth = 128f * cell.Width / cell.Height;
            GraphicsState head = g.Save();
            g.TranslateTransform(pose.HeadCenter.X, pose.HeadCenter.Y); g.RotateTransform(pose.HeadAngle);
            g.DrawImage(faces, new RectangleF(-headWidth / 2, -64, headWidth, 128), cell, GraphicsUnit.Pixel);
            PointF[] corners = { new PointF(-headWidth / 2, -64), new PointF(headWidth / 2, -64), new PointF(-headWidth / 2, 64), new PointF(headWidth / 2, 64) };
            using (Matrix transform = g.Transform) transform.TransformPoints(corners);
            g.Restore(head);
            using (Matrix transform = g.Transform) { transform.Invert(); transform.TransformPoints(corners); }
            if (variant == 7 && pose.Reaction > 0)
            {
                PointF[] front = new PointF[8]; Array.Copy(shape, shape.Length - front.Length, front, 0, front.Length);
                DrawRope(g, front, Color.FromArgb(79, 47, 29), true);
            }
            if (variant == 4 && age >= 1.16 && age <= 1.82)
            {
                RectangleF phone = new RectangleF(pose.FreeHand.X - 8, pose.FreeHand.Y - 23, 17, 26);
                using (Brush black = new SolidBrush(Ink)) g.FillRectangle(black, phone);
                using (Font font = new Font("Consolas", 11, FontStyle.Bold))
                using (Brush white = new SolidBrush(Color.White)) g.DrawString("X", font, white, phone.X + 2, phone.Y + 2);
            }
            double impactTime = variant == 4 || variant == 6 || variant == 7 ? .78 : WhipChoreography.ImpactSeconds;
            if (age >= impactTime && age < impactTime + .16)
            {
                PointF tip = shape[shape.Length - 1];
                int alpha = (int)(255 * (1 - (age - impactTime) / .16));
                using (Pen hit = new Pen(Color.FromArgb(alpha, Orange), 2.5f))
                {
                    g.DrawLine(hit, tip.X + 6, tip.Y - 5, tip.X + 17, tip.Y - 12);
                    g.DrawLine(hit, tip.X + 8, tip.Y + 1, tip.X + 22, tip.Y + 3);
                }
                using (Font font = new Font("Microsoft YaHei", 14, FontStyle.Bold))
                using (Brush ink = new SolidBrush(Color.FromArgb(alpha, Ink)))
                    g.DrawString(variant == 4 ? "啊?" : "啪", font, ink, tip.X + 8, tip.Y - 30);
            }
            DrawBubble(g, message, HeadBounds(corners), new RectangleF(-164, -140, 640, 420));
            DrawFooter(g, "暴躁 " + state.AngerLevel + "/6  ·  点击安抚  ·  今日记录 " + dailyCount, Orange, 260);
            olderRope = lastRope; lastRope = shape;
        }

        static void DrawPoseLeg(Graphics g, Pen pen, PointF hip, PointF foot, int side, float dangling = 0)
        {
            PointF knee = new PointF((hip.X + foot.X) / 2 + side * 7, (hip.Y + foot.Y) / 2 - 3);
            knee.X += (hip.X + side * 17 - knee.X) * dangling;
            knee.Y += (hip.Y + 18 - knee.Y) * dangling;
            g.DrawLines(pen, new PointF[] { hip, knee, foot });
            g.DrawLine(pen, foot.X - 4, foot.Y - dangling * 4, foot.X + 9 - dangling * 5, foot.Y + dangling * 4);
        }

        static PointF[] ComedyRope(PointF[] physical, WhipPose pose, int variant, double age)
        {
            PointF[] shape = (PointF[])physical.Clone();
            if (variant != 4 && variant != 6 && variant != 7) return shape;
            float weight = Ease((age - .58) / .20) * (1 - Ease((age - 1.08) / .22));
            if (weight == 0) return shape;
            for (int i = 2; i < shape.Length; i++)
            {
                float t = (float)i / (shape.Length - 1);
                PointF target;
                PointF root = pose.HandleTip;
                if (variant == 7)
                {
                    float left = pose.LeftFoot.X, right = pose.RightFoot.X, floor = pose.LeftFoot.Y;
                    float u = t * 3;
                    if (u < 1) target = Cubic(root, new PointF(root.X + pose.HandleDirection.X * 65, root.Y + pose.HandleDirection.Y * 65), new PointF(right + 42, floor - 25), new PointF(right + 20, floor), u);
                    else if (u < 2) target = Cubic(new PointF(right + 20, floor), new PointF(right + 30, floor + 20), new PointF(left - 32, floor + 20), new PointF(left - 20, floor), u - 1);
                    else target = Cubic(new PointF(left - 20, floor), new PointF(left - 24, floor - 20), new PointF(right + 18, floor - 20), new PointF(right + 6, floor - 3), u - 2);
                }
                else
                {
                    PointF tip = variant == 6 ? new PointF(385, -20) : new PointF(pose.Hip.X - 15, pose.Hip.Y - 1);
                    PointF control = variant == 6 ? new PointF(340, -20) : new PointF(311, 254);
                    target = Cubic(root, new PointF(root.X + pose.HandleDirection.X * 65, root.Y + pose.HandleDirection.Y * 65), control, tip, t);
                }
                shape[i] = new PointF(shape[i].X + (target.X - shape[i].X) * weight, shape[i].Y + (target.Y - shape[i].Y) * weight);
            }
            return shape;
        }

        static PointF Cubic(PointF a, PointF b, PointF c, PointF d, float t)
        {
            float u = 1 - t;
            return new PointF(u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * c.X + t * t * t * d.X,
                u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * c.Y + t * t * t * d.Y);
        }
        static float Ease(double value) { float t = (float)Math.Max(0, Math.Min(1, value)); return t * t * (3 - 2 * t); }

        static void DrawRope(Graphics g, PointF[] points, Color color, bool outline)
        {
            using (GraphicsPath full = RopePath(points, 0, points.Length - 1))
            {
                if (outline)
                    using (Pen halo = new Pen(Color.FromArgb(235, 246, 242, 233), 6) { LineJoin = LineJoin.Round })
                        g.DrawPath(halo, full);
                for (int section = 0; section < 4; section++)
                {
                    int begin = section * (points.Length - 1) / 4, end = (section + 1) * (points.Length - 1) / 4;
                    using (GraphicsPath path = RopePath(points, begin, end))
                    using (Pen core = new Pen(color, 3.6f - section * .65f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawPath(core, path);
                }
            }
        }
        static GraphicsPath RopePath(PointF[] points, int start, int end)
        {
            GraphicsPath path = new GraphicsPath();
            for (int i = start; i < end; i++)
            {
                PointF a = points[Math.Max(0, i - 1)], b = points[i], c = points[i + 1], d = points[Math.Min(points.Length - 1, i + 2)];
                path.AddBezier(b, new PointF(b.X + (c.X - a.X) / 6, b.Y + (c.Y - a.Y) / 6),
                    new PointF(c.X - (d.X - b.X) / 6, c.Y - (d.Y - b.Y) / 6), c);
            }
            return path;
        }

        internal static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath(); float d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90); path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }
        public void Dispose() { faces.Dispose(); }
    }
}
