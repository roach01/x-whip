using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace XWhip
{
    public sealed class AppSettings
    {
        public int IntervalMinutes = 15;
        public bool Sound = false;
        public bool Roam = true;
        public bool ScreenShakeEnabled = true;
        public bool PromotionEnabled = true;
        public int Size = 180;

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "XWhipPet", "settings.xml");
            }
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    using (FileStream stream = File.OpenRead(SettingsPath))
                    {
                        AppSettings settings = (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(stream);
                        if (settings == null) return new AppSettings();
                        settings.Clamp();
                        return settings;
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (InvalidOperationException) { }
            return new AppSettings();
        }

        public void Save()
        {
            Clamp();
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            using (FileStream stream = File.Create(SettingsPath))
                new XmlSerializer(typeof(AppSettings)).Serialize(stream, this);
        }

        private void Clamp()
        {
            IntervalMinutes = Math.Max(1, Math.Min(180, IntervalMinutes));
            Size = Math.Max(140, Math.Min(240, Size));
        }
    }

    public sealed class SettingsForm : Form
    {
        private static readonly Color Cream = Color.FromArgb(244, 241, 234);
        private static readonly Color Ink = Color.FromArgb(29, 28, 26);
        private static readonly Color Orange = Color.FromArgb(242, 101, 53);
        private static readonly Color Muted = Color.FromArgb(117, 111, 102);
        private readonly Font bodyFont = new Font("Microsoft YaHei", 9.5f);
        private readonly Font titleFont = new Font("Microsoft YaHei", 25, FontStyle.Bold);
        private readonly Font boldFont = new Font("Microsoft YaHei", 10, FontStyle.Bold);
        private readonly Font smallFont = new Font("Microsoft YaHei", 8.5f);
        private readonly NumericUpDown interval;
        private readonly CheckBox sound;
        private readonly CheckBox roam;
        private readonly CheckBox screenShake;
        private readonly CheckBox promotion;
        private readonly TrackBar sizeSlider;
        private readonly Label sizeValue;
        private readonly CountdownTimeline timeline;

        public AppSettings Values { get; private set; }

        public SettingsForm(AppSettings source)
        {
            Values = new AppSettings
            {
                IntervalMinutes = Math.Max(1, Math.Min(180, source.IntervalMinutes)),
                Sound = source.Sound,
                Roam = source.Roam,
                ScreenShakeEnabled = source.ScreenShakeEnabled,
                PromotionEnabled = source.PromotionEnabled,
                Size = Math.Max(140, Math.Min(240, source.Size))
            };

            Text = "X / WHIP · 设置";
            ClientSize = new Size(520, 580);
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Cream;
            ForeColor = Ink;
            Font = bodyFont;

            AddLabel("X / WHIP", 28, 18, 420, 45, titleFont, Ink);
            AddLabel("你的桌角摸鱼搭子", 30, 65, 420, 24, bodyFont, Muted);
            Panel divider = new Panel();
            divider.SetBounds(28, 98, 464, 1);
            divider.BackColor = Color.FromArgb(211, 205, 194);
            Controls.Add(divider);

            AddLabel("冷却时间", 28, 116, 220, 28, boldFont, Ink);
            interval = new NumericUpDown();
            interval.SetBounds(365, 113, 82, 30);
            interval.Minimum = 1;
            interval.Maximum = 180;
            interval.Value = Values.IntervalMinutes;
            interval.BackColor = Color.White;
            interval.BorderStyle = BorderStyle.FixedSingle;
            interval.TextAlign = HorizontalAlignment.Center;
            Controls.Add(interval);
            AddLabel("分钟", 455, 117, 40, 28, bodyFont, Muted);
            AddLabel("每轮从冷静开始，到点后连续抽鞭，逐渐加速。", 28, 153, 464, 25, smallFont, Muted);

            timeline = new CountdownTimeline();
            timeline.SetBounds(28, 180, 464, 105);
            timeline.Font = smallFont;
            timeline.BackColor = Cream;
            timeline.Minutes = Values.IntervalMinutes;
            Controls.Add(timeline);
            interval.ValueChanged += delegate { timeline.Minutes = (int)interval.Value; };

            screenShake = AddCheckBox("屏幕轻轻抖一下", 28, 292, Values.ScreenShakeEnabled);
            screenShake.Width = 224;
            sound = AddCheckBox("播放抽鞭提示音", 268, 292, Values.Sound);
            sound.Width = 224;
            AddLabel("仅 4–6 级轻轻晃，不闪屏；台词偶尔冒出来。", 28, 324, 464, 20, smallFont, Muted);
            roam = AddCheckBox("闲时在桌面左右移动", 28, 344, Values.Roam);
            roam.Width = 224;
            promotion = AddCheckBox("推荐卡皮芭拉好友", 268, 344, Values.PromotionEnabled);
            promotion.Width = 224;

            AddLabel("宠物大小", 28, 378, 160, 24, boldFont, Ink);
            sizeValue = AddLabel(Values.Size + " px", 423, 378, 69, 24, bodyFont, Muted);
            sizeValue.TextAlign = ContentAlignment.MiddleRight;
            sizeSlider = new TrackBar();
            sizeSlider.SetBounds(20, 404, 480, 33);
            sizeSlider.Minimum = 140;
            sizeSlider.Maximum = 240;
            sizeSlider.TickFrequency = 20;
            sizeSlider.SmallChange = 10;
            sizeSlider.LargeChange = 20;
            sizeSlider.Value = Values.Size;
            sizeSlider.AutoSize = false;
            sizeSlider.BackColor = Cream;
            sizeSlider.ValueChanged += delegate { sizeValue.Text = sizeSlider.Value + " px"; };
            Controls.Add(sizeSlider);

            AddLabel("点一下记一笔，收鞭后重新冷却。拖动不计数。\n只要开着，他就一直陪你刷 X。", 28, 451, 464, 38, smallFont, Ink);
            AddLabel("今日记录北京时间零点清零，同日重启保留。\n关注、发帖、回复由你在 X 中手动完成。", 28, 497, 464, 38, smallFont, Muted);

            Button cancel = AddButton("取消", 28, 541, 80, 32, false);
            cancel.DialogResult = DialogResult.Cancel;
            Button preview = AddButton("试抽一次", 252, 538, 112, 36, false);
            preview.Click += delegate { ReadValues(); DialogResult = DialogResult.Retry; };
            Button save = AddButton("保存设置", 376, 538, 116, 36, true);
            save.Click += delegate { ReadValues(); DialogResult = DialogResult.OK; };
            AcceptButton = save;
            CancelButton = cancel;
        }

        private void ReadValues()
        {
            Values.IntervalMinutes = (int)interval.Value;
            Values.Sound = sound.Checked;
            Values.Roam = roam.Checked;
            Values.ScreenShakeEnabled = screenShake.Checked;
            Values.PromotionEnabled = promotion.Checked;
            Values.Size = sizeSlider.Value;
        }

        private Label AddLabel(string text, int x, int y, int width, int height, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.SetBounds(x, y, width, height);
            label.Font = font;
            label.ForeColor = color;
            Controls.Add(label);
            return label;
        }

        private CheckBox AddCheckBox(string text, int x, int y, bool value)
        {
            CheckBox checkBox = new CheckBox();
            checkBox.Text = text;
            checkBox.SetBounds(x, y, 464, 27);
            checkBox.Checked = value;
            checkBox.FlatStyle = FlatStyle.Standard;
            Controls.Add(checkBox);
            return checkBox;
        }

        private Button AddButton(string text, int x, int y, int width, int height, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(x, y, width, height);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(200, 192, 180);
            button.BackColor = primary ? Orange : Cream;
            button.ForeColor = primary ? Color.White : Ink;
            button.Font = boldFont;
            button.Cursor = Cursors.Hand;
            Controls.Add(button);
            return button;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                bodyFont.Dispose();
                titleFont.Dispose();
                boldFont.Dispose();
                smallFont.Dispose();
            }
        }

        private sealed class CountdownTimeline : Control
        {
            private int minutes;
            public int Minutes
            {
                get { return minutes; }
                set { minutes = value; Invalidate(); }
            }

            public CountdownTimeline()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                TextRenderer.DrawText(e.Graphics, "暴躁进度", Font, new Point(0, 0), Ink);
                double[] bounds = { 0, 8.0 / 15, 12.0 / 15, 14.0 / 15, 1 };
                Color[] colors = { Color.FromArgb(206, 200, 189), Color.FromArgb(244, 173, 116), Orange, Color.FromArgb(193, 55, 28) };
                string[] names = { "摸鱼", "盯你", "催促", "蓄力" };
                string[] percents = { "0–53.3%", "53.3–80%", "80–93.3%", "93.3–100%" };
                for (int i = 0; i < 4; i++)
                {
                    int left = (int)Math.Round(bounds[i] * Width);
                    int right = (int)Math.Round(bounds[i + 1] * Width);
                    using (SolidBrush brush = new SolidBrush(colors[i]))
                        e.Graphics.FillRectangle(brush, left, 28, right - left - 2, 9);
                    int column = i * Width / 4;
                    Rectangle nameRect = new Rectangle(column, 46, Width / 4, 20);
                    TextRenderer.DrawText(e.Graphics, names[i], Font, nameRect, Ink,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                    string range = FormatTime(bounds[i]) + "–" + FormatTime(bounds[i + 1]);
                    TextRenderer.DrawText(e.Graphics, range, Font, new Rectangle(column, 66, Width / 4, 19), Muted,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(e.Graphics, percents[i], Font, new Rectangle(column, 84, Width / 4, 19), Muted,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
                }
            }

            private string FormatTime(double fraction)
            {
                int seconds = (int)Math.Round(minutes * 60 * fraction);
                return (seconds / 60).ToString() + ":" + (seconds % 60).ToString("00");
            }
        }
    }
}
