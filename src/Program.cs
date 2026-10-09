using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace XWhip
{
    internal static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread] static void Main(string[] args)
        {
            SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && args[0] == "--preview") { Preview(args.Length > 1 ? args[1] : "preview"); return; }
            if (args.Length > 0 && args[0] == "--smoke-test") { Smoke(args.Length > 1 ? args[1] : "smoke.txt"); return; }
            bool owns;
            using (Mutex singleton = new Mutex(true, "Local\\XWhipPetPrototype", out owns))
            {
                if (!owns) { MessageBox.Show("宠物已经在桌面上了。可在系统托盘找到 X / WHIP。", "X / WHIP"); return; }
                try { Application.Run(new PetForm(AppSettings.Load())); }
                catch (Exception ex) { MessageBox.Show("启动失败：" + ex.Message, "X / WHIP", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        static void Preview(string folder)
        {
            Directory.CreateDirectory(folder);
            DateTime start = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
            using (PetRenderer renderer = new PetRenderer())
            using (Bitmap board = new Bitmap(1280, 370))
            using (Graphics g = Graphics.FromImage(board))
            {
                using (Icon icon = renderer.CreateIcon())
                using (FileStream stream = File.Create(Path.Combine(folder, "app.ico"))) icon.Save(stream);
                g.Clear(Color.FromArgb(244, 241, 234));
                int[] seconds = { 0, 9 * 60, 13 * 60, 14 * 60 + 20 };
                string[] labels = { "01  摸鱼  ·  0–8 分钟", "02  盯你  ·  8–12 分钟", "03  催促  ·  12–14 分钟", "04  蓄力  ·  14–15 分钟" };
                for (int i = 0; i < 4; i++)
                {
                    PetState state = new PetState(15, start);
                    using (Bitmap frame = renderer.Render(state, start.AddSeconds(seconds[i]), 4, -1, null, false, 180))
                        g.DrawImageUnscaled(frame, i * 320, 50);
                    using (Font font = new Font("Microsoft YaHei", 10, FontStyle.Bold))
                    using (Brush ink = new SolidBrush(PetRenderer.Ink)) g.DrawString(labels[i], font, ink, i * 320 + 25, 337);
                }
                board.Save(Path.Combine(folder, "countdown-stages.png"), ImageFormat.Png);
                string framesFolder = Path.Combine(folder, "frames"); Directory.CreateDirectory(framesFolder);
                string[] poses = { "叉腰甩", "后仰甩", "双手抡", "弓步抽", "跺脚抽", "跳起抽", "绕身旋转", "自缠踉跄" };
                using (Bitmap gallery = new Bitmap(2400, 880))
                using (Graphics galleryGraphics = Graphics.FromImage(gallery))
                using (Font label = new Font("Microsoft YaHei", 14, FontStyle.Bold))
                using (Brush ink = new SolidBrush(PetRenderer.Ink))
                {
                    galleryGraphics.Clear(Color.FromArgb(244, 241, 234));
                    for (int n = 0; n < 8; n++)
                    {
                        PetState pose = PreviewStrike(n + 1, start);
                        using (Bitmap frame = renderer.Render(pose, start, 4, .29, RageQuotes.Lines[n % 6], false, 180))
                            galleryGraphics.DrawImage(frame, new Rectangle(n % 4 * 600, n / 4 * 440 + 35, 600, 394));
                        galleryGraphics.DrawString((n + 1).ToString("00") + "  " + poses[n], label, ink, n % 4 * 600 + 24, n / 4 * 440 + 5);
                    }
                    gallery.Save(Path.Combine(folder, "rage-poses.png"), ImageFormat.Png);
                }
                PetState demo = new PetState(15, start);
                using (Bitmap levels = new Bitmap(1800, 880))
                using (Graphics levelGraphics = Graphics.FromImage(levels))
                using (Font label = new Font("Microsoft YaHei", 14, FontStyle.Bold))
                using (Brush ink = new SolidBrush(PetRenderer.Ink))
                {
                    levelGraphics.Clear(Color.FromArgb(244, 241, 234));
                    for (int level = 1; level <= 6; level++)
                    {
                        PetState pose = PreviewStrike((level - 1) * 3 + 1, start);
                        using (Bitmap frame = renderer.Render(pose, start, 4, .29, RageQuotes.Lines[(level - 1) % 5], false, 180, false, 1, 0, 7))
                            levelGraphics.DrawImage(frame, new Rectangle((level - 1) % 3 * 600, (level - 1) / 3 * 440 + 35, 600, 394));
                        levelGraphics.DrawString(level + " 级" + (level >= 4 ? " · 前台窗口会轻抖" : ""), label, ink, (level - 1) % 3 * 600 + 24, (level - 1) / 3 * 440 + 5);
                    }
                    levels.Save(Path.Combine(folder, "rage-levels.png"), ImageFormat.Png);
                }
                RoamMotion previewMotion = new RoamMotion(16); previewMotion.Reset(290);
                for (int i = 0; i < 960; i++)
                {
                    DateTime now = start.AddSeconds(i / 30.0);
                    double strikeAge = -1; string text = null, caption;
                    if (i < 180)
                    {
                        previewMotion.Tick(1 / 30.0, 0, 260, 330);
                        caption = previewMotion.Moving ? (previewMotion.Direction > 0 ? "闲逛 · 向右走" : "闲逛 · 向左走") : "闲逛 · 停下来歇会儿";
                    }
                    else if (i < 720)
                    {
                        int level = (i - 180) / 90 + 1;
                        if ((i - 180) % 90 == 0) demo = PreviewStrike((level - 1) * 3 + 1, now);
                        previewMotion.Hold(); strikeAge = (i - 180) % 30 / 30.0;
                        text = (i - 180) % 90 < 60 ? RageQuotes.Lines[level - 1] : null;
                        caption = "暴躁 " + level + "/6" + (level >= 4 ? " · 前台窗口轻抖，无闪屏" : "");
                    }
                    else
                    {
                        if (i == 720) demo.Calm(now);
                        if (i == 780) demo.Rest(now);
                        previewMotion.Hold();
                        text = i < 780 ? "今日 +1。好的老板，我收手。" : null;
                        caption = i < 780 ? "点击 +1 · 重新计时" : "下班 · 墨镜躺平";
                    }
                    using (Bitmap frame = renderer.Render(demo, now, i / 30.0, strikeAge, text, false, 180, previewMotion.Moving, previewMotion.Direction, previewMotion.WalkPhase, i >= 720 ? 8 : 7))
                    using (Bitmap canvas = new Bitmap(900, 690))
                    using (Graphics surface = Graphics.FromImage(canvas))
                    {
                        surface.Clear(Color.FromArgb(244, 241, 234));
                        Point offset = PetRenderer.ActorOffset(demo.Mode, 180);
                        surface.DrawImageUnscaled(frame, (i < 180 ? (int)previewMotion.X : 290) - offset.X, 280 - offset.Y);
                        using (Font title = new Font("Microsoft YaHei", 15, FontStyle.Bold))
                        using (Brush ink = new SolidBrush(PetRenderer.Ink)) surface.DrawString("X / WHIP", title, ink, 24, 16);
                        using (Font label = new Font("Microsoft YaHei", 11))
                        using (Brush ink = new SolidBrush(PetRenderer.Ink)) surface.DrawString(caption, label, ink, 24, 50);
                        canvas.Save(Path.Combine(framesFolder, i.ToString("0000") + ".png"), ImageFormat.Png);
                    }
                }
                PetState resting = new PetState(15, start); resting.Rest(start);
                using (Bitmap rest = renderer.Render(resting, start, 3, -1, null, false, 240))
                using (Bitmap canvas = new Bitmap(480, 440))
                using (Graphics surface = Graphics.FromImage(canvas))
                {
                    surface.Clear(Color.FromArgb(244, 241, 234)); surface.DrawImageUnscaled(rest, 27, 42);
                    using (Font title = new Font("Microsoft YaHei", 15, FontStyle.Bold))
                    using (Brush ink = new SolidBrush(PetRenderer.Ink)) surface.DrawString("OFF DUTY / 下班躺平", title, ink, 24, 12);
                    canvas.Save(Path.Combine(folder, "off-duty.png"), ImageFormat.Png);
                }
            }
            using (SettingsForm settings = new SettingsForm(new AppSettings()))
            using (Bitmap bitmap = new Bitmap(settings.Width, settings.Height))
            {
                settings.StartPosition = FormStartPosition.Manual; settings.Location = new Point(-10000, -10000);
                settings.Show(); Application.DoEvents();
                settings.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(folder, "settings.png")); settings.Close();
            }
        }

        static PetState PreviewStrike(int count, DateTime now)
        {
            DateTime earlier = now.AddSeconds(-count * 4);
            PetState state = new PetState(15, earlier); state.TestWhip(earlier);
            for (int n = 0; n < count; n++) state.Tick(earlier.AddSeconds(n * 4));
            return state;
        }

        static void Smoke(string report)
        {
            using (PetForm form = new PetForm(new AppSettings { Roam = false, Sound = false, WorkHoursEnabled = false }, true))
            {
                form.Show(); Application.DoEvents(); form.TriggerTest();
                form.PumpOnce();
                if (form.State.Mode != PetMode.Whipping || form.State.StrikeCount != 1) throw new Exception("First strike failed");
                form.ClickForTest();
                if (form.State.Mode != PetMode.Cooling || form.State.StrikeCount != 0) throw new Exception("Click did not calm");
                form.State.TogglePause(DateTime.UtcNow); form.PumpOnce();
                if (form.State.Mode != PetMode.Paused) throw new Exception("Pause failed");
                if (form.DailyCount != 1) throw new Exception("Click did not increment progress");
                form.ClockOut(); form.ClickForTest();
                if (form.State.Mode != PetMode.Resting || form.DailyCount != 2) throw new Exception("Off-duty click must count and stay resting");
                form.TriggerTest(); form.PumpOnce();
                if (form.State.Mode != PetMode.Whipping) throw new Exception("Off-duty trial did not change state");
                form.TriggerTest(); form.PumpOnce();
                if (form.State.Mode != PetMode.Whipping) throw new Exception("Repeated trial lost override");
                form.ClickForTest();
                if (form.State.Mode != PetMode.Resting || form.DailyCount != 3) throw new Exception("Trial click must return to rest and count");
                form.ClockIn();
                if (form.State.Mode != PetMode.Cooling) throw new Exception("Manual clock-in failed");
                bool hasFriendLink = false;
                foreach (ToolStripItem item in form.ContextMenuStrip.Items)
                    if (item.Text == "查看我的好友卡皮芭拉 ↗" && item.Enabled) hasFriendLink = true;
                if (!hasFriendLink) throw new Exception("Permanent friend link missing");
                form.Close();
            }
            using (PetForm scheduled = new PetForm(new AppSettings { Roam = false, Sound = false }, true))
            {
                DateTime utc = new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);
                DateTime local = new DateTime(2026, 10, 9, 18, 0, 0);
                scheduled.State.TestWhip(utc); scheduled.State.Tick(utc);
                scheduled.ApplyWorkHours(utc, local);
                if (scheduled.State.Mode != PetMode.Resting) throw new Exception("Scheduled clock-out did not interrupt whipping");
                scheduled.ApplyWorkHours(utc.AddHours(15), local.AddHours(15));
                if (scheduled.State.Mode != PetMode.Cooling || scheduled.State.Remaining(utc.AddHours(15)).TotalMinutes != 15)
                    throw new Exception("Next shift did not start a full cooldown");
            }
            using (PetForm hiddenPromotion = new PetForm(new AppSettings { PromotionEnabled = false, WorkHoursEnabled = false }, true))
            {
                foreach (ToolStripItem item in hiddenPromotion.ContextMenuStrip.Items)
                    if (item.Text.Contains("卡皮芭拉") && item.Available) throw new Exception("Promotion remained visible");
            }
            using (Form target = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(-10000, -10000), Size = new Size(400, 300) })
            using (WindowShake effect = new WindowShake())
            {
                target.Show(); Application.DoEvents(); Point original = target.Location;
                effect.BeginOnWindow(target.Handle, 3, 0);
                if (effect.Active) throw new Exception("Level 3 must not shake");
                effect.BeginOnWindow(target.Handle, 4, 0); effect.Tick(.04);
                if (!effect.Active || target.Location == original) throw new Exception("Level 4 shake did not move test window");
                effect.Tick(.25);
                if (effect.Active || target.Location != original) throw new Exception("Shake did not restore window");
                effect.BeginOnWindow(target.Handle, 6, 1); effect.Tick(1.04); effect.Stop();
                if (target.Location != original) throw new Exception("Interrupted shake did not restore window");
                target.Close();
            }
            File.WriteAllText(report, "PASS: layered alpha, six-level rendering, click +1, off-duty click +1, off-duty trial/repeat/calm, promotion disabled, work hours, next shift, level-4 threshold, window-shake movement/restoration/cancellation, disposal. No desktop capture or flashing overlay.\r\n");
        }
    }

    internal sealed class PetForm : Form
    {
        internal readonly PetState State;
        AppSettings settings;
        readonly PetRenderer renderer = new PetRenderer();
        readonly System.Windows.Forms.Timer frameTimer = new System.Windows.Forms.Timer { Interval = 33 };
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly NotifyIcon tray;
        readonly Icon trayIcon;
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly RoamMotion motion = new RoamMotion(Environment.TickCount);
        readonly RageQuotes quotes = new RageQuotes(Environment.TickCount ^ 0x541);
        readonly SpeechSchedule speech = new SpeechSchedule(Environment.TickCount ^ 0x745);
        readonly DailyProgress progress;
        readonly WindowShake shake = new WindowShake();
        readonly ToolStripMenuItem friendLink;
        double lastFrame, lastStrike = -100, messageUntil, celebrationUntil;
        string message;
        Point downCursor, downLocation;
        bool mouseHeld, dragged, configuring, impactDone;
        PetMode canvasMode = PetMode.Cooling;
        int renderedSize = 180;
        DateTime? manualRestUntil, manualWorkUntil;
        static readonly TimeZoneInfo WorkTimeZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
        readonly bool testing;
        bool trialOutsideHours, progressSaveFailed;
        internal int DailyCount { get { return progress.Count; } }

        internal PetForm(AppSettings values, bool test = false)
        {
            settings = values; testing = test; State = new PetState(settings.IntervalMinutes, DateTime.UtcNow);
            string progressPath = test ? Path.Combine(Path.GetTempPath(), "XWhip-test-" + Guid.NewGuid().ToString("N"), "daily.xml") :
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XWhipPet", "daily.xml");
            try { progress = new DailyProgress(progressPath, WorkLocal(DateTime.UtcNow)); }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException || ex is FormatException)) throw;
                if (File.Exists(progressPath)) File.Move(progressPath, progressPath + ".corrupt-" + DateTime.UtcNow.Ticks);
                progress = new DailyProgress(progressPath, WorkLocal(DateTime.UtcNow));
            }
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
            StartPosition = FormStartPosition.Manual; Cursor = Cursors.Hand; AutoScaleMode = AutoScaleMode.None;
            ResizePet(); Rectangle desktop = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(desktop.Right - Width - 35, desktop.Bottom - Height - 8); motion.Reset(Left);
            menu.Font = new Font("Microsoft YaHei", 9); menu.BackColor = Color.FromArgb(246, 242, 233);
            AddMenu("去 X 营业 ↗", delegate { OpenX(); });
            friendLink = AddMenu("查看我的好友卡皮芭拉 ↗", delegate { OpenFriend(); });
            friendLink.Visible = settings.PromotionEnabled;
            AddMenu("完成一次互动 ✓", delegate { Complete(); });
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem pause = AddMenu("暂停 / 继续", delegate { State.TogglePause(DateTime.UtcNow); });
            ToolStripMenuItem clockOut = AddMenu("今日下班 · 躺平", delegate { ClockOut(); });
            ToolStripMenuItem clockIn = AddMenu("现在上班", delegate { ClockIn(); });
            AddMenu("重新计时", delegate { Calm("重新计时，先放你一马。"); });
            AddMenu("试抽一次", delegate { TriggerTest(); });
            menu.Items.Add(new ToolStripSeparator());
            AddMenu("设置…", delegate { ShowSettings(); });
            AddMenu("退出", delegate { Close(); });
            menu.Opening += delegate
            {
                bool resting = State.Mode == PetMode.Resting;
                pause.Text = State.Mode == PetMode.Paused ? "继续提醒" : "暂停提醒";
                pause.Enabled = !resting; clockOut.Enabled = !resting; clockIn.Enabled = resting;
            };
            ContextMenuStrip = menu;
            trayIcon = renderer.CreateIcon();
            tray = new NotifyIcon { Icon = trayIcon, Text = "X / WHIP · 右键设置", ContextMenuStrip = menu, Visible = !testing };
            tray.DoubleClick += delegate { ShowSettings(); };
            frameTimer.Tick += delegate { PumpOnce(); };
            Shown += delegate
            {
                Say("左键 +1 · 右键设置 · 拖动搬家", 3); PumpOnce(); frameTimer.Start();
                if (!testing) { tray.ShowBalloonTip(5000, "X / WHIP 已到岗", "默认 15 分钟冷却。右键宠物可设置时间、试抽、打开 X。", ToolTipIcon.Info); }
            };
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        { get { CreateParams p = base.CreateParams; p.ExStyle |= 0x80000 | 0x80 | 0x08000000; return p; } }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }
            base.WndProc(ref m);
        }

        ToolStripMenuItem AddMenu(string label, Action action)
        { ToolStripMenuItem item = new ToolStripMenuItem(label); item.Click += delegate { action(); }; menu.Items.Add(item); return item; }

        void ResizePet()
        {
            Point oldOffset = PetRenderer.ActorOffset(canvasMode, renderedSize);
            Point anchor = new Point(Left + oldOffset.X, Top + oldOffset.Y);
            ClientSize = PetRenderer.CanvasSize(State.Mode, settings.Size);
            Point newOffset = PetRenderer.ActorOffset(State.Mode, settings.Size);
            Location = new Point(anchor.X - newOffset.X, anchor.Y - newOffset.Y);
            canvasMode = State.Mode; renderedSize = settings.Size; motion.Reset(Left);
        }
        void Say(string text, double seconds) { message = text; messageUntil = clock.Elapsed.TotalSeconds + seconds; speech.ResetIdle(clock.Elapsed.TotalSeconds); }
        internal void TriggerTest()
        {
            trialOutsideHours = trialOutsideHours || State.Mode == PetMode.Resting;
            shake.Stop();
            Say(null, 0); lastStrike = -100; State.TestWhip(DateTime.UtcNow);
        }
        void Calm(string text)
        {
            shake.Stop();
            if (trialOutsideHours) { trialOutsideHours = false; State.Rest(DateTime.UtcNow); }
            State.Calm(DateTime.UtcNow); lastStrike = -100;
            Say(State.Mode == PetMode.Resting ? (text.Contains("+1") ? "今日 +1。下班继续躺。" : "下班免谈，明天再卷。") : text, 2.8); PumpOnce();
        }
        void Complete()
        {
            RecordCompletion();
            if (trialOutsideHours) { Calm("好的老板，我继续躺。"); return; }
            if (State.Mode == PetMode.Resting) { Say("今日完成 +1，继续躺。", 2); return; }
            shake.Stop();
            State.Complete(DateTime.UtcNow); lastStrike = -100; celebrationUntil = clock.Elapsed.TotalSeconds + 2; Say("营业完成！好的老板。", 3);
        }

        void RecordCompletion()
        {
            try { progress.RecordClick(WorkLocal(DateTime.UtcNow)); progressSaveFailed = false; }
            catch (IOException ex) { ProgressFailure(ex); }
            catch (UnauthorizedAccessException ex) { ProgressFailure(ex); }
        }
        void ProgressFailure(Exception ex)
        {
            if (progressSaveFailed) return;
            progressSaveFailed = true;
            if (!testing) MessageBox.Show("本次 +1 已记入，未能保存到磁盘：" + ex.Message, "X / WHIP");
        }

        static DateTime WorkLocal(DateTime utcNow) { return TimeZoneInfo.ConvertTimeFromUtc(utcNow, WorkTimeZone); }
        internal void ClockOut()
        {
            trialOutsideHours = false; shake.Stop();
            DateTime now = DateTime.UtcNow;
            manualWorkUntil = null;
            manualRestUntil = settings.WorkHoursEnabled ? WorkHours.NextStart(WorkLocal(now), settings.WorkStartMinutes) : DateTime.MaxValue;
            State.Rest(now); lastStrike = -100; celebrationUntil = 0; Say(null, 0); PumpOnce();
        }
        internal void ClockIn()
        {
            trialOutsideHours = false; shake.Stop();
            DateTime now = DateTime.UtcNow, local = WorkLocal(now);
            manualRestUntil = null;
            DateTime end = local.Date.AddMinutes(settings.WorkEndMinutes);
            if (end <= local) end = end.AddDays(1);
            manualWorkUntil = settings.WorkHoursEnabled ? end : DateTime.MaxValue;
            State.StartWork(now); Say("上班了，先给你一轮冷却。", 3); PumpOnce();
        }
        internal void ApplyWorkHours(DateTime utcNow, DateTime localNow)
        {
            if (manualRestUntil.HasValue && localNow >= manualRestUntil.Value) manualRestUntil = null;
            if (manualWorkUntil.HasValue && localNow >= manualWorkUntil.Value) manualWorkUntil = null;
            if (trialOutsideHours)
            {
                if (!settings.WorkHoursEnabled || !WorkHours.IsWorking(localNow, settings.WorkStartMinutes, settings.WorkEndMinutes) || manualRestUntil.HasValue) return;
                trialOutsideHours = false; shake.Stop(); State.Rest(utcNow);
            }
            bool shouldRest = manualRestUntil.HasValue || (!manualWorkUntil.HasValue && settings.WorkHoursEnabled &&
                !WorkHours.IsWorking(localNow, settings.WorkStartMinutes, settings.WorkEndMinutes));
            if (shouldRest && State.Mode != PetMode.Resting)
            { shake.Stop(); State.Rest(utcNow); lastStrike = -100; celebrationUntil = 0; Say(null, 0); }
            else if (!shouldRest && State.Mode == PetMode.Resting)
            { State.StartWork(utcNow); Say("上班了，先给你一轮冷却。", 3); }
        }

        internal void PumpOnce()
        {
            if (IsDisposed || !IsHandleCreated) return;
            DateTime now = DateTime.UtcNow; double time = clock.Elapsed.TotalSeconds;
            double delta = Math.Min(.1, time - lastFrame); lastFrame = time;
            try { progress.Refresh(WorkLocal(now)); }
            catch (IOException ex) { ProgressFailure(ex); }
            catch (UnauthorizedAccessException ex) { ProgressFailure(ex); }
            ApplyWorkHours(now, WorkLocal(now));
            if (State.Tick(now))
            {
                lastStrike = time; impactDone = false;
                if (time >= messageUntil && speech.TryWhip(time)) Say(quotes.Next(settings.PromotionEnabled), SpeechSchedule.WhipDisplaySeconds);
            }
            if (canvasMode != State.Mode) { ResizePet(); KeepOnScreen(); }
            double strikeAge = time - lastStrike;
            if (!impactDone && State.Mode == PetMode.Whipping && strikeAge >= .24 && strikeAge < .8)
            {
                impactDone = true;
                if (settings.Sound && !testing) System.Media.SystemSounds.Exclamation.Play();
                if (settings.ScreenShakeEnabled && State.AngerLevel >= 4 && !testing && !menu.Visible && !configuring)
                    shake.Begin(Handle, State.AngerLevel, time);
            }
            if (State.Mode != PetMode.Whipping || !settings.ScreenShakeEnabled) shake.Stop();
            else shake.Tick(time);
            if (State.Mode == PetMode.Cooling && time >= messageUntil && speech.TryIdle(time))
                Say(quotes.Next(settings.PromotionEnabled), SpeechSchedule.IdleDisplaySeconds);
            if (settings.Roam && State.Mode == PetMode.Cooling && State.CountdownStage(now) < 3 && !mouseHeld && !menu.Visible && !configuring)
            {
                Rectangle area = Screen.FromControl(this).WorkingArea;
                motion.Tick(delta, State.CountdownStage(now), area.Left, Math.Max(area.Left, area.Right - Width));
                Left = (int)motion.X;
            }
            else motion.Hold();
            using (Bitmap frame = renderer.Render(State, now, time, strikeAge, time < messageUntil ? message : null, time < celebrationUntil, settings.Size, motion.Moving, motion.Direction, motion.WalkPhase, progress.Count))
                Native.Present(this, frame);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) { mouseHeld = true; dragged = false; downCursor = Cursor.Position; downLocation = Location; Capture = true; }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!mouseHeld) return;
            Point cursor = Cursor.Position; int dx = cursor.X - downCursor.X, dy = cursor.Y - downCursor.Y;
            if (Math.Abs(dx) + Math.Abs(dy) > 5) dragged = true;
            if (dragged) Location = new Point(downLocation.X + dx, downLocation.Y + dy);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && mouseHeld)
            {
                mouseHeld = false; Capture = false;
                if (!dragged) { RecordCompletion(); Calm("今日 +1。好的老板，我收手。"); }
                else KeepOnScreen();
            }
        }
        internal void ClickForTest()
        { OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 150, 120, 0)); OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 150, 120, 0)); }

        void OpenX()
        {
            OpenPage("https://x.com/home", "去吧，评论记得带点内容。");
        }
        void OpenFriend()
        {
            OpenPage("https://x.com/oh_roach", "去看看淡定的卡皮芭拉。");
        }
        void OpenPage(string url, string caption)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); Say(caption, 4); }
            catch (System.ComponentModel.Win32Exception ex) { MessageBox.Show("无法打开浏览器：" + ex.Message, "X / WHIP"); }
        }
        void ShowSettings()
        {
            if (configuring) return; configuring = true;
            try
            {
                using (SettingsForm form = new SettingsForm(settings))
                {
                    DialogResult result = form.ShowDialog();
                    if (result == DialogResult.OK || result == DialogResult.Retry)
                    {
                        int previousInterval = settings.IntervalMinutes;
                        bool changedHours = settings.WorkHoursEnabled != form.Values.WorkHoursEnabled ||
                            settings.WorkStartMinutes != form.Values.WorkStartMinutes || settings.WorkEndMinutes != form.Values.WorkEndMinutes;
                        settings = form.Values;
                        friendLink.Visible = settings.PromotionEnabled;
                        if (!settings.PromotionEnabled && message != null && message.Contains("卡皮芭拉")) Say(null, 0);
                        if (!settings.ScreenShakeEnabled) shake.Stop();
                        if (settings.IntervalMinutes != previousInterval) State.SetInterval(settings.IntervalMinutes, DateTime.UtcNow);
                        if (changedHours) { manualRestUntil = null; manualWorkUntil = null; }
                        ApplyWorkHours(DateTime.UtcNow, WorkLocal(DateTime.UtcNow));
                        ResizePet(); KeepOnScreen();
                        try { settings.Save(); }
                        catch (IOException ex) { SaveFailure(ex); }
                        catch (UnauthorizedAccessException ex) { SaveFailure(ex); }
                        catch (InvalidOperationException ex) { SaveFailure(ex); }
                        if (result == DialogResult.Retry) TriggerTest();
                    }
                }
            }
            finally { configuring = false; }
        }
        void SaveFailure(Exception ex) { MessageBox.Show("设置本次已生效，但未能保存：" + ex.Message, "X / WHIP"); }
        void KeepOnScreen()
        {
            Rectangle area = Screen.FromControl(this).WorkingArea;
            Left = Math.Max(area.Left, Math.Min(Left, area.Right - Width));
            Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)); motion.Reset(Left);
        }
        void DisplayChanged(object sender, EventArgs e) { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)KeepOnScreen); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
                frameTimer.Dispose(); shake.Dispose(); tray.Visible = false; tray.Dispose(); trayIcon.Dispose(); menu.Font.Dispose(); menu.Dispose(); renderer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
