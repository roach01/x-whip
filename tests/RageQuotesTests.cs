using System;
using System.Collections.Generic;
using XWhip;

internal static class RageQuotesTests
{
    private static int Main()
    {
        try
        {
            string[] expected =
            {
                "今天关注到400了吗？",
                "今天发帖到200了吗？",
                "今天互动到1500了吗？",
                "今天交到好朋友了吗？",
                "今天追热点了吗？",
                "今天看“淡定的卡皮芭拉”首页了吗？"
            };
            Check(RageQuotes.Lines.Length == 6, "six quotes required");
            for (int index = 0; index < expected.Length; index++)
                Check(expected[index] == RageQuotes.Lines[index], "quote text changed");

            var quotes = new RageQuotes(123);
            var sameSeed = new RageQuotes(123);
            var seen = new HashSet<string>();
            string previous = null;
            for (int index = 0; index < 1000; index++)
            {
                string line = quotes.Next();
                Check(line != previous, "consecutive duplicate at " + index);
                Check(Array.IndexOf(expected, line) >= 0, "unexpected quote");
                Check(line == sameSeed.Next(), "seed does not reproduce sequence");
                seen.Add(line);
                previous = line;
            }
            Check(seen.Count == 6, "all six quotes must be covered");
            seen.Clear();
            for (int index = 0; index < 1000; index++)
            {
                string line = quotes.Next(false);
                Check(line != previous, "disabled promotion consecutive duplicate");
                Check(line != expected[5], "disabled promotion was displayed");
                seen.Add(line);
                previous = line;
            }
            Check(seen.Count == 5, "disabled promotion must cover first five quotes");
            for (int index = 0; index < 1000; index++)
            {
                bool promotionEnabled = index % 3 == 0;
                string line = quotes.Next(promotionEnabled);
                Check(line != previous, "promotion toggle consecutive duplicate");
                Check(promotionEnabled || line != expected[5], "promotion toggle displayed disabled promotion");
                previous = line;
            }
            Console.WriteLine("PASS: 3000 picks, no consecutive duplicates, both pool sizes covered, promotion toggle respected, deterministic seed");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            return 1;
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
