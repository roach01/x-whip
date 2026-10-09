using System;
using System.Globalization;
using System.IO;
using System.Xml;

namespace XWhip
{
    public sealed class DailyProgress
    {
        private readonly string path;

        public int Count { get; private set; }
        public DateTime Day { get; private set; }

        public DailyProgress(string path, DateTime beijingNow)
        {
            this.path = path;
            Day = beijingNow.Date;
            if (!File.Exists(path)) return;
            var document = new XmlDocument();
            document.Load(path);
            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != "dailyProgress")
                throw new FormatException("Invalid daily progress file.");
            DateTime savedDay = DateTime.ParseExact(root.GetAttribute("day"), "yyyy-MM-dd", CultureInfo.InvariantCulture);
            int savedCount = Int32.Parse(root.GetAttribute("count"), CultureInfo.InvariantCulture);
            if (savedCount < 0) throw new FormatException("Invalid daily progress count.");
            if (savedDay == Day) Count = savedCount;
            else Save();
        }

        public bool Refresh(DateTime beijingNow)
        {
            if (Day == beijingNow.Date) return false;
            Day = beijingNow.Date;
            Count = 0;
            Save();
            return true;
        }

        public void RecordClick(DateTime beijingNow)
        {
            if (Day != beijingNow.Date)
            {
                Day = beijingNow.Date;
                Count = 0;
            }
            Count++;
            Save();
        }

        private void Save()
        {
            string folder = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            var document = new XmlDocument();
            XmlElement root = document.CreateElement("dailyProgress");
            root.SetAttribute("day", Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            root.SetAttribute("count", Count.ToString(CultureInfo.InvariantCulture));
            document.AppendChild(root);
            document.Save(path);
        }
    }
}
