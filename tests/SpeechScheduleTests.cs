using System;
using XWhip;

internal static class SpeechScheduleTests
{
    private static int passed;

    private static int Main()
    {
        try
        {
            Run("Idle starts quietly and speaks within twenty to forty seconds", InitialIdle);
            Run("Idle intervals remain within the requested range", RecurringIdle);
            Run("Reset idle postpones chatter after interaction", ResetIdle);
            Run("Whip speech has a six-second minimum gap", WhipThrottle);
            Run("Rapid whipping leaves most strikes silent", RapidWhipping);
            Run("Mode changes cannot overlap speech", CrossMode);
            Run("Fixed seed reproduces idle timings", Deterministic);
            Run("Long time jumps produce a single speech event", LongJump);
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
    {
        test(); passed++;
        Console.WriteLine("PASS " + name);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static double FindIdle(SpeechSchedule schedule, double start, double end)
    {
        for (int step = 0; start + step * 0.1 <= end + 0.000001; step++)
        {
            double now = start + step * 0.1;
            if (schedule.TryIdle(now)) return now;
        }
        throw new Exception("No idle speech within deadline");
    }

    private static void InitialIdle()
    {
        var schedule = new SpeechSchedule(19, 100);
        Check(!schedule.TryIdle(100), "no immediate idle chatter");
        Check(!schedule.TryIdle(119.999), "twenty-second minimum");
        double first = FindIdle(schedule, 120, 140.1);
        Check(first >= 120 && first <= 140.1, "first idle delay range");
        Check(!schedule.TryIdle(first), "no duplicate event at same instant");
        Check(SpeechSchedule.IdleDisplaySeconds == 3, "idle display duration");
        Check(SpeechSchedule.WhipDisplaySeconds == 2.8, "whip display duration");
    }

    private static void RecurringIdle()
    {
        var schedule = new SpeechSchedule(26);
        double previous = 0;
        for (int eventIndex = 0; eventIndex < 30; eventIndex++)
        {
            Check(!schedule.TryIdle(previous + 19.999), "idle minimum interval");
            double now = FindIdle(schedule, previous + 20, previous + 40.1);
            Check(now - previous >= 20 && now - previous <= 40.1, "idle interval bounds");
            previous = now;
        }
    }

    private static void ResetIdle()
    {
        var schedule = new SpeechSchedule(3);
        schedule.ResetIdle(19.5);
        Check(!schedule.TryIdle(39.499), "interaction resets minimum wait");
        FindIdle(schedule, 39.5, 59.6);
    }

    private static void WhipThrottle()
    {
        var schedule = new SpeechSchedule(2);
        Check(schedule.TryWhip(0), "first whip can speak");
        Check(!schedule.TryWhip(2.8), "end of bubble does not permit next bubble yet");
        Check(!schedule.TryWhip(5.999), "six-second minimum");
        Check(schedule.TryWhip(6), "six-second boundary can speak");
        Check(!schedule.TryWhip(6), "same strike cannot show duplicate bubble");
        Check(schedule.TryWhip(12), "next six-second boundary");
    }

    private static void RapidWhipping()
    {
        var schedule = new SpeechSchedule(5);
        int spoken = 0;
        double previous = -100;
        for (int strike = 0; strike < 100; strike++)
        {
            double now = strike * 0.8;
            if (!schedule.TryWhip(now)) continue;
            Check(now - previous >= 6 - 0.000001, "rapid whip minimum gap");
            previous = now; spoken++;
        }
        Check(spoken >= 10 && spoken <= 14, "most of one hundred rapid strikes remain silent");
    }

    private static void CrossMode()
    {
        var schedule = new SpeechSchedule(7);
        double idle = FindIdle(schedule, 20, 40.1);
        Check(!schedule.TryWhip(idle + 0.1), "idle bubble blocks immediate whip bubble");
        Check(schedule.TryWhip(idle + 6), "whip after gap");
        Check(!schedule.TryIdle(idle + 25.999), "whip restarts idle minimum");
        FindIdle(schedule, idle + 26, idle + 46.1);
    }

    private static void Deterministic()
    {
        var first = new SpeechSchedule(17);
        var second = new SpeechSchedule(17);
        for (int frame = 0; frame < 10000; frame++)
            Check(first.TryIdle(frame * 0.1) == second.TryIdle(frame * 0.1), "same seed timing mismatch");
    }

    private static void LongJump()
    {
        var schedule = new SpeechSchedule(4);
        Check(schedule.TryIdle(10000), "overdue idle event");
        Check(!schedule.TryIdle(10000), "long jump does not create speech burst");
        Check(!schedule.TryWhip(10000), "long jump does not permit cross-mode burst");
    }
}
