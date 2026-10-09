using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using XWhip;

internal static class DailyProgressTests
{
    private static readonly DateTime Beijing = new DateTime(2026, 10, 9, 15, 0, 0);
    private static readonly List<string> files = new List<string>();
    private static int passed;

    private static int Main()
    {
        try
        {
            Run("Every click increments and survives restart", ClicksAndRestart);
            Run("Beijing midnight clears and persists count", MidnightReset);
            Run("A click on a new day begins at one", NewDayClick);
            Run("Restart on a new day clears old progress", RestartNewDay);
            Run("Refresh during same day does not rewrite file", SameDayRefresh);
            Run("Corrupt records are reported to the caller", CorruptRecord);
            Run("Persistence failures reach the caller", SaveFailure);
            Console.WriteLine("PASS: " + passed + " tests");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            return 1;
        }
        finally
        {
            foreach (string path in files)
            {
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path)) Directory.Delete(path);
            }
        }
    }

    private static void Run(string name, Action test)
    {
        test(); passed++;
        Console.WriteLine("PASS " + name);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static string NewPath()
    {
        string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "daily-progress-test-data");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".xml");
        files.Add(path);
        return path;
    }

    private static void ClicksAndRestart()
    {
        string path = NewPath();
        var progress = new DailyProgress(path, Beijing);
        Check(progress.Count == 0 && progress.Day == Beijing.Date, "new record starts at zero");
        progress.RecordClick(Beijing);
        progress.RecordClick(Beijing.AddSeconds(1));
        Check(progress.Count == 2, "each click increments");
        var restarted = new DailyProgress(path, Beijing.AddHours(1));
        Check(restarted.Count == 2, "restart keeps count");
        restarted.RecordClick(Beijing.AddHours(2));
        Check(new DailyProgress(path, Beijing).Count == 3, "next click persists after restart");
    }

    private static void MidnightReset()
    {
        string path = NewPath();
        DateTime lastSecond = Beijing.Date.AddDays(1).AddSeconds(-1);
        var progress = new DailyProgress(path, lastSecond);
        progress.RecordClick(lastSecond);
        Check(progress.Refresh(lastSecond.AddSeconds(1)), "midnight changes date");
        Check(progress.Count == 0 && progress.Day == Beijing.Date.AddDays(1), "midnight clears count");
        Check(new DailyProgress(path, lastSecond.AddSeconds(2)).Count == 0, "midnight reset persisted");
    }

    private static void NewDayClick()
    {
        string path = NewPath();
        var progress = new DailyProgress(path, Beijing);
        progress.RecordClick(Beijing); progress.RecordClick(Beijing);
        progress.RecordClick(Beijing.AddDays(1));
        Check(progress.Count == 1, "first new-day click is one");
        Check(new DailyProgress(path, Beijing.AddDays(1)).Count == 1, "new-day click persists");
    }

    private static void RestartNewDay()
    {
        string path = NewPath();
        var progress = new DailyProgress(path, Beijing);
        progress.RecordClick(Beijing);
        var restarted = new DailyProgress(path, Beijing.AddDays(1));
        Check(restarted.Count == 0, "new-day restart clears");
        var document = new XmlDocument(); document.Load(path);
        Check(document.DocumentElement.GetAttribute("day") == "2026-10-10", "new-day restart persisted date");
        Check(document.DocumentElement.GetAttribute("count") == "0", "new-day restart persisted zero");
    }

    private static void SameDayRefresh()
    {
        string path = NewPath();
        var progress = new DailyProgress(path, Beijing);
        progress.RecordClick(Beijing);
        DateTime stamp = Beijing.AddDays(-2);
        File.SetLastWriteTimeUtc(path, stamp);
        DateTime storedStamp = File.GetLastWriteTimeUtc(path);
        Check(!progress.Refresh(Beijing.AddHours(1)), "same-day refresh reports no reset");
        Check(progress.Count == 1, "same-day refresh preserves count");
        Check(File.GetLastWriteTimeUtc(path) == storedStamp, "same-day refresh avoids write");
    }

    private static void CorruptRecord()
    {
        string path = NewPath();
        File.WriteAllText(path, "<dailyProgress day=\"2026-10-09\" count=\"-1\" />");
        bool threw = false;
        try { new DailyProgress(path, Beijing); }
        catch (FormatException) { threw = true; }
        Check(threw, "invalid saved count should reach caller");
        File.WriteAllText(path, "<dailyProgress");
        threw = false;
        try { new DailyProgress(path, Beijing); }
        catch (XmlException) { threw = true; }
        Check(threw, "malformed XML should reach caller");
    }

    private static void SaveFailure()
    {
        string path = NewPath();
        Directory.CreateDirectory(path);
        var progress = new DailyProgress(path, Beijing);
        bool threw = false;
        try { progress.RecordClick(Beijing); }
        catch (UnauthorizedAccessException) { threw = true; }
        catch (IOException) { threw = true; }
        Check(threw, "save failure should reach caller");
        Check(progress.Count == 1, "click remains counted in memory if save fails");
    }
}
