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
                bool mayWalk = state.Mode == PetMode.Cooling;
                double elapsed = Double.IsNaN(lastRenderTime) || time < lastRenderTime ? 0 : Math.Min(.15, time - lastRenderTime);
                if (Double.IsNaN(lastRenderTime) || time < lastRenderTime) { walkBlend = 0; frozenWalkPhase = walkPhase; }
                lastRenderTime = time;
                if (!mayWalk) walkBlend = 0;
                else walkBlend = Math.Max(0, Math.Min(1, walkBlend + (walking ? 1 : -1) * (float)(elapsed / .15)));
                if (walking && mayWalk) frozenWalkPhase = walkPhase;
                walkPhase = frozenWalkPhase;
                walking = mayWalk && walkBlend > 0;
                if (state.Mode == PetMode.Resting) { DrawRest(g, time, message, dailyCount); return frame; }
                int stage = state.CountdownStage(now);
                bool whip = state.Mode == PetMode.Whipping;
                bool paused = state.Mode == PetMode.Paused;
                double swing = strikeAge >= 0 && strikeAge < .5 ? Math.Sin(strikeAge / .5 * Math.PI) : 0;
                walking = walking && !paused && !whip;
                float bounce = walking ? -(float)Math.Abs(Math.Sin(walkPhase * 2)) * 1.5f * walkBlend : 0;
                if (celebration) bounce -= (float)(Math.Abs(Math.Sin(time * 5)) * 18);
                int variant = Math.Max(0, state.StrikeCount - 1) % 8;
                float tilt = 0, stretch = 1, wind = 0, snap = 0;
                PointF whipHand = new PointF(200, 140), whipElbow = new PointF(182, 170);
                if (whip)
                {
                    wind = strikeAge >= 0 && strikeAge < .24 ? (float)Math.Sin(strikeAge / .24 * Math.PI / 2) : 0;
                    snap = strikeAge >= .24 && strikeAge < .5 ? (float)(1 - (strikeAge - .24) / .26) : 0;
                    float strength = .7f + state.AngerLevel * .1f;
                    float[] backs = { -9, -36, -7, 7, -10, -12, 18, 25 }, forwards = { 12, 25, 11, 9, 17, 14, -16, -28 }, jumps = { 6, 16, 14, 6, 0, 80, 28, 12 };
                    tilt = (backs[variant] * wind + forwards[variant] * snap) * strength;
                    if (state.AngerLevel >= 5) tilt += (float)Math.Sin(time * (14 + state.AngerLevel * 3)) * (state.AngerLevel - 3) * (float)swing;
                    if (variant == 3) tilt = Math.Max(-14, Math.Min(12, tilt));
                    float deform = .08f + Math.Max(0, state.AngerLevel - 3) * .025f;
                    stretch = 1 + deform * wind - deform * snap;
                    bounce = -(float)Math.Min(80, swing * jumps[variant] * strength);
                    StrikeHand(variant, (float)swing, out whipHand, out whipElbow);
                    float hx = (whipHand.X + 7 - 156) / stretch, hy = (whipHand.Y - 11 - 215) * stretch;
                    double angle = tilt * Math.PI / 180;
                    DrawWhip(g, state.AngerLevel, variant, strikeAge, new PointF(320 + hx * (float)Math.Cos(angle) - hy * (float)Math.Sin(angle), 355 + bounce + hx * (float)Math.Sin(angle) + hy * (float)Math.Cos(angle)));
                    g.TranslateTransform(164, 140);
                }
                using (Brush shadow = new SolidBrush(Color.FromArgb(35, 0, 0, 0))) g.FillEllipse(shadow, 124, 252, 70, 8);
                g.TranslateTransform(0, whip ? 0 : bounce);

                Color mood = whip || stage >= 2 ? Orange : Ink;
                GraphicsState actor = g.Save();
                if (whip) { g.TranslateTransform(0, bounce); g.TranslateTransform(156, 215); g.RotateTransform(tilt); g.ScaleTransform(1 / stretch, stretch); g.TranslateTransform(-156, -215); }

                using (Pen pen = new Pen(Ink, 4.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    g.DrawLine(pen, 156, 170, 156, 217);
                    float gait = walking ? (float)Math.Sin(walkPhase) * walkBlend : 0;
                    if (whip) DrawStrikeBody(g, pen, variant, (float)swing, wind, snap, whipHand, whipElbow, state.AngerLevel);
                    else
                    {
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
                            if (swing > .05) g.DrawBezier(rope, handX + 7, handY - 11, 267, 70, 295, 154, 266 - (float)swing * 4, 226);
                            else g.DrawBezier(rope, handX + 7, handY - 11, 240, 109, 265, 161, 234, 198);
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
                }

                int[] relaxed = { 0, 1, 2, 8, 10, 11, 9 }, furious = { 5, 6, 7, 13, 14, 15 };
                int expression = paused ? 1 : whip ? furious[Math.Min(5, state.AngerLevel - 1)] : stage == 0 ? relaxed[(int)(time / 12) % relaxed.Length] : stage == 1 ? ((int)(time / 10) % 2 == 0 ? 3 : 9) : stage == 2 ? ((int)(time / 10) % 3 == 2 ? 13 : 4 + (int)(time / 10) % 2) : ((int)(time / 10) % 2 == 0 ? 6 : 12);
                if (!whip && !paused && !String.IsNullOrEmpty(message) && (celebration || message.Contains("收手") || message.Contains("放你"))) expression = 0;
                Rectangle cell = faceRects[expression];
                float lean = whip ? (float)swing * 5 : facing * 3;
                float headHeight = 128;
                float headWidth = headHeight * cell.Width / cell.Height;
                g.DrawImage(faces, new RectangleF(158 - headWidth / 2 + lean, 176 - headHeight, headWidth, headHeight), cell, GraphicsUnit.Pixel);
                if (stage >= 2 && (whip || String.IsNullOrEmpty(message)))
                {
                    using (Pen steam = new Pen(Orange, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        float jitter = (float)Math.Sin(time * 8) * 3;
                        g.DrawBezier(steam, 223, 103, 234, 96, 219, 87, 229, 79 + jitter);
                        if (stage >= 3) g.DrawBezier(steam, 234, 113, 245, 105, 231, 95, 239, 87 - jitter);
                        if (whip && state.AngerLevel >= 3) g.DrawBezier(steam, 96, 102, 85, 96, 97, 85, 89, 74 + jitter);
                    }
                }
                PointF[] headCorners = { new PointF(158 - headWidth / 2 + lean, 48), new PointF(158 + headWidth / 2 + lean, 48), new PointF(158 - headWidth / 2 + lean, 176), new PointF(158 + headWidth / 2 + lean, 176) };
                using (Matrix transform = g.Transform) transform.TransformPoints(headCorners);
                g.Restore(actor);
                using (Matrix transform = g.Transform) { transform.Invert(); transform.TransformPoints(headCorners); }
                DrawBubble(g, message, HeadBounds(headCorners), whip ? new RectangleF(-164, -140, 640, 420) : new RectangleF(0, -bounce, 320, 280));

                string status;
                if (paused) status = "ZZZ  ·  已暂停";
                else if (whip) status = "暴躁 " + state.AngerLevel + "/6  ·  点击安抚";
                else
                {
                    TimeSpan remaining = state.Remaining(now);
                    string[] names = { "摸鱼", "盯你", "催促", "蓄力" };
                    status = names[Math.Min(stage, 3)] + "  ·  " + ((int)remaining.TotalMinutes).ToString("00") + ":" + remaining.Seconds.ToString("00");
                }
                float statusY = whip ? 260 : 263;
                DrawFooter(g, status + "  ·  今日完成 " + dailyCount, mood, statusY);
                if (!whip && swing > .5)
                {
                    using (Pen impact = new Pen(Orange, 3))
                    {
                        g.DrawLine(impact, 267, 225, 284, 231); g.DrawLine(impact, 265, 234, 275, 249); g.DrawLine(impact, 261, 224, 276, 214);
                    }
                }
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

        static void StrikeHand(int variant, float swing, out PointF hand, out PointF elbow)
        {
            PointF[] hands = { new PointF(204, 160), new PointF(207, 132), new PointF(178, 57), new PointF(236, 155), new PointF(199, 132), new PointF(213, 138), new PointF(208, 158), new PointF(114, 186) };
            PointF[] elbows = { new PointF(184, 171), new PointF(186, 164), new PointF(183, 115), new PointF(198, 165), new PointF(179, 155), new PointF(187, 160), new PointF(188, 172), new PointF(132, 176) };
            float[] dx = { 16, 22, 28, 12, 16, 6, 21, -6 }, dy = { 39, 65, 116, 44, 55, 36, 35, 8 };
            hand = new PointF(hands[variant].X + dx[variant] * swing, hands[variant].Y + dy[variant] * swing); elbow = elbows[variant];
        }

        static void DrawStrikeBody(Graphics g, Pen pen, int variant, float swing, float wind, float snap, PointF hand, PointF elbow, int level)
        {
            if (variant == 3)
            {
                g.DrawLines(pen, new PointF[] { new PointF(156, 217), new PointF(181, 231), new PointF(212, 249) });
                g.DrawLines(pen, new PointF[] { new PointF(156, 217), new PointF(127, 223), new PointF(102, 249) });
                g.DrawLine(pen, 205, 250, 221, 250); g.DrawLine(pen, 96, 250, 112, 250);
            }
            else if (variant == 5 && swing > .1f)
            {
                g.DrawLines(pen, new PointF[] { new PointF(156, 217), new PointF(141, 229), new PointF(128, 221 + (1 - swing) * 28) });
                g.DrawLines(pen, new PointF[] { new PointF(156, 217), new PointF(173, 229), new PointF(189, 224 + (1 - swing) * 25) });
            }
            else
            {
                DrawLeg(g, pen, variant == 7 ? 124 : 143, 250 - (variant == 4 ? wind * 13 : 0), 1, variant == 4);
                DrawLeg(g, pen, variant == 7 ? 185 : 172, 250 - (variant == 7 ? wind * 12 : 0), 1, variant == 7);
            }
            PointF[] offElbows = { new PointF(134, 199), new PointF(129, 179), new PointF(128, 122), new PointF(128, 188), new PointF(134, 199), new PointF(147, 159), new PointF(128, 175), new PointF(179, 175) };
            PointF[] offHands = { new PointF(146, 207), new PointF(113, 192), hand, new PointF(108, 174), new PointF(145, 207), new PointF(122, 143), new PointF(110, 166), new PointF(203, 164) };
            g.DrawLines(pen, new PointF[] { new PointF(153, 184), offElbows[variant], offHands[variant] });
            g.DrawLines(pen, new PointF[] { new PointF(160, 187), elbow, hand });
            if (level >= 4 && swing > .05f)
            using (Pen arms = new Pen(Color.FromArgb(90 + (level - 4) * 35, Orange), 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                for (int i = 0; i < level - 3; i++)
                    for (int side = -1; side <= 1; side += 2)
                        g.DrawLines(arms, new PointF[] { new PointF(156, 186), new PointF(156 + side * (33 + i * 7), 187 - i * 8), new PointF(156 + side * (58 + i * 12), 164 - i * 17 + swing * 18) });
            using (Pen handle = new Pen(Color.FromArgb(137, 71, 43), 6) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(handle, hand.X - 3, hand.Y + 4, hand.X + 7, hand.Y - 11);
            if (variant == 4 && snap > 0) using (Pen stomp = new Pen(Color.FromArgb((int)(snap * 180), Orange), 3)) { g.DrawLine(stomp, 122, 254, 132, 248); g.DrawLine(stomp, 153, 251, 162, 255); }
        }

        void DrawWhip(Graphics g, int level, int variant, double age, PointF hand)
        {
            bool active = age >= 0 && age < .5;
            float phase = active ? (float)(age / .5) : 0;
            using (Pen rope = new Pen(Ink, 3.5f + level) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (!active) { g.DrawBezier(rope, hand.X, hand.Y, hand.X + 90, hand.Y - 35, 475, 315, 443, 371); return; }
                for (int trail = 2; trail >= 0; trail--)
                using (Pen blur = new Pen(Color.FromArgb(trail == 0 ? 255 : 60 - trail * 15, trail == 0 ? Ink : Orange), trail == 0 ? 4 + level : 9 + level * 2))
                {
                    float delta = trail * 13, endY = phase < .48f ? 64 + phase * 300 : 334;
                    switch (variant)
                    {
                    case 0: g.DrawBezier(blur, hand.X, hand.Y, 575 - delta, 31 + delta, 616, 145 + delta, 570 - delta, endY); break;
                    case 1: g.DrawBezier(blur, hand.X, hand.Y, 55 + delta, 77, 567, 25 + delta, 557, endY); break;
                    case 2: g.DrawBezier(blur, hand.X, hand.Y, 107 + delta, 52 + delta, 587, 55 + delta, 550, 323); break;
                    case 3: g.DrawBezier(blur, hand.X, hand.Y, 604, 192 + delta, 145, 386 - delta, 557, 323); break;
                    case 4: g.DrawBezier(blur, hand.X, hand.Y, 478, 374 - delta, 612, 253, 557, 323); break;
                    case 5: g.DrawBezier(blur, hand.X, hand.Y, 572, 74 + delta, 626 - delta, 283, 557, 323); break;
                    case 6:
                        g.DrawBezier(blur, hand.X, hand.Y, 614, 32 + delta, 44 + delta, 18, 90 + delta, 219);
                        g.DrawArc(blur, 90 + delta, 64 + delta, 484 - delta * 2, 287 - delta, -25 + phase * 380, 330);
                        g.DrawArc(blur, 129 + delta, 95 + delta, 410 - delta * 2, 223 - delta, 160 - phase * 430, 320);
                        g.DrawBezier(blur, 519, 165, 610, 207, 602, 269, 558, 330);
                        break;
                    default:
                        g.DrawBezier(blur, hand.X, hand.Y, 174, 174, 467, 95 + delta, 454 - delta, 255);
                        g.DrawArc(blur, 224 + delta, 133 + delta, 207 - delta, 188 - delta, 25 - phase * 430, 340);
                        g.DrawArc(blur, 246 + delta, 164 + delta, 164 - delta, 159 - delta, 120 + phase * 490, 345);
                        g.DrawBezier(blur, 259, 311, 327, 389, 429, 363, 465, 320); break;
                    }
                }
                if (level >= 4)
                {
                    int extra = level == 4 ? 1 : level == 5 ? 2 : 4;
                    for (int i = 0; i < extra; i++)
                    {
                        float end = 90 + i * (460f / Math.Max(1, extra - 1));
                        using (Pen glow = new Pen(Color.FromArgb(100 + level * 10, Orange), 10)) g.DrawBezier(glow, hand.X, hand.Y, end, 70 + i % 2 * 95, end + 42, 277, end, 350 - i % 2 * 35);
                        using (Pen lash = new Pen(Ink, 3 + level * .5f)) g.DrawBezier(lash, hand.X, hand.Y, end, 70 + i % 2 * 95, end + 42, 277, end, 350 - i % 2 * 35);
                    }
                }
                if (level >= 3)
                {
                    if (variant == 6 || variant == 7) {
                    PointF[] spiral = new PointF[65];
                    for (int i = 0; i < spiral.Length; i++) { double a = i * .24 + phase * 13; float r = 158 - i * 1.8f; spiral[i] = new PointF(331 + (float)Math.Cos(a) * r * 1.45f, 232 + (float)Math.Sin(a) * r * .82f); }
                    using (Pen fire = new Pen(Color.FromArgb(190, Orange), 5)) g.DrawLines(fire, spiral);
                    }
                    using (Pen speed = new Pen(Color.FromArgb(115, Ink), 3))
                        for (int i = 0; i < 12; i++) { double a = i * Math.PI / 6; float x = (float)Math.Cos(a), y = (float)Math.Sin(a); g.DrawLine(speed, 320 + x * 194, 222 + y * 148, 320 + x * 271, 222 + y * 180); }
                    if (variant != 7) using (Pen lightning = new Pen(Orange, 7) { LineJoin = LineJoin.Bevel }) g.DrawLines(lightning, new PointF[] { new PointF(480, 127), new PointF(544, 168), new PointF(521, 207), new PointF(579, 252), new PointF(550, 318) });
                }
            }
            if (age < .24 || age > .43) return;
            int alpha = (int)(255 * (1 - (age - .24) / .19)); float radius = level >= 3 ? 71 + (level - 3) * 3 : level == 2 ? 44 : 27;
            float impactX = variant == 7 ? 465 : level >= 4 ? 546 : 557;
            PointF[] burst = new PointF[24];
            for (int i = 0; i < burst.Length; i++) { double a = i * Math.PI / 12; float r = i % 2 == 0 ? radius : radius * .56f; burst[i] = new PointF(impactX + (float)Math.Cos(a) * r, 323 + (float)Math.Sin(a) * r); }
            using (Brush fire = new SolidBrush(Color.FromArgb(alpha, Orange))) g.FillPolygon(fire, burst);
            using (Pen outline = new Pen(Color.FromArgb(alpha, Ink), 3)) g.DrawPolygon(outline, burst);
            using (Font font = new Font("Microsoft YaHei", level >= 3 ? 32 + (level - 3) * 2 : level == 2 ? 22 : 14, FontStyle.Bold))
            using (Brush ink = new SolidBrush(Color.FromArgb(alpha, Ink)))
            using (StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(level >= 5 ? "啪!!!" : level >= 3 ? "啪!!" : "啪!", font, ink, new RectangleF(impactX - radius, 323 - radius, radius * 2, radius * 2), centered);
        }

        void DrawRest(Graphics g, double time, string message, int dailyCount)
        {
            float breath = (float)Math.Sin(time * 1.7) * 1.4f;
            using (Brush pad = new SolidBrush(Color.FromArgb(236, 221, 191))) g.FillEllipse(pad, 78, 215, 208, 30);
            using (Pen pen = new Pen(Ink, 4.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawLines(pen, new PointF[] { new PointF(86, 225), new PointF(268, 225), new PointF(279, 205) });
                g.DrawLine(pen, 110, 225, 102, 248); g.DrawLine(pen, 249, 225, 260, 247);
                g.DrawLine(pen, 143, 204 + breath, 223, 208 + breath);
                g.DrawLines(pen, new PointF[] { new PointF(166, 205 + breath), new PointF(153, 182), new PointF(122, 185) });
                g.DrawLines(pen, new PointF[] { new PointF(178, 206 + breath), new PointF(165, 190), new PointF(131, 193) });
                g.DrawLines(pen, new PointF[] { new PointF(223, 208 + breath), new PointF(249, 186), new PointF(270, 207) });
                g.DrawLines(pen, new PointF[] { new PointF(223, 208 + breath), new PointF(249, 220), new PointF(277, 216) });
                g.DrawLine(pen, 268, 207, 281, 205); g.DrawLine(pen, 276, 216, 286, 209);
            }
            GraphicsState head = g.Save(); g.TranslateTransform(119, 169 + breath); g.RotateTransform(-55);
            Rectangle cell = faceRects[0]; float width = 112f * cell.Width / cell.Height;
            g.DrawImage(faces, new RectangleF(-width / 2, -56, width, 112), cell, GraphicsUnit.Pixel);
            using (Brush lenses = new SolidBrush(Color.FromArgb(248, 14, 16, 20))) { g.FillRectangle(lenses, -30, -5, 27, 17); g.FillRectangle(lenses, 3, -5, 27, 17); }
            using (Pen rim = new Pen(Ink, 3)) { g.DrawLine(rim, -35, -5, 35, -5); g.DrawLine(rim, -3, 1, 3, 1); }
            using (Pen shine = new Pen(Color.FromArgb(125, 255, 255, 255), 2)) { g.DrawLine(shine, -25, -1, -18, -1); g.DrawLine(shine, 8, -1, 15, -1); }
            PointF[] headCorners = { new PointF(-width / 2, -56), new PointF(width / 2, -56), new PointF(-width / 2, 56), new PointF(width / 2, 56) };
            using (Matrix transform = g.Transform) transform.TransformPoints(headCorners);
            g.Restore(head);
            using (Matrix transform = g.Transform) { transform.Invert(); transform.TransformPoints(headCorners); }
            DrawBubble(g, message, HeadBounds(headCorners), new RectangleF(0, 0, 320, 280));
            using (Font font = new Font("Microsoft YaHei", 10, FontStyle.Bold))
            using (Brush ink = new SolidBrush(Ink))
            using (StringFormat centered = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("Z z z", font, ink, new RectangleF(235, 115 + breath, 76, 22), centered);
            DrawFooter(g, "OFF DUTY · 已下班  ·  今日完成 " + dailyCount, Ink, 263);
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
