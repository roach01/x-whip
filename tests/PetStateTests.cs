using System;
using XWhip;

internal static class PetStateTests
{
    private static readonly DateTime Start = new DateTime(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);
    private static int passed;

    private static int Main()
    {
        try
        {
            Run("Initial state and interval clamps", InitialState);
            Run("First strike exactly at deadline", Deadline);
            Run("Strike delays, escalation, and anger cap", StrikeEscalation);
            Run("Large time jumps emit one strike per tick", LargeTimeJump);
            Run("Calm resets full cooldown and strikes", CalmReset);
            Run("Cooling interval changes restart timer", CoolingInterval);
            Run("Whipping interval changes preserve strike schedule", WhippingInterval);
            Run("Test whip emits on next tick", ManualWhip);
            Run("Default cooldown stage boundaries", DefaultStages);
            Run("Custom cooldown stages scale proportionally", CustomStages);
            Run("Whipping countdown stage and calm reset", ModeStages);
            Run("Repeated calm restarts a running cooldown", RepeatedCalm);
            Run("Cooldown and whipping continue across midnight", AcrossMidnight);
            Run("Beijing evening continues cooldown and whipping", BeijingEvening);
            Run("Beijing midnight preserves cooldown deadline", BeijingMidnight);
            Run("Changing interval preserves maximum rage", MaximumRageInterval);
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
        test();
        passed++;
        Console.WriteLine("PASS " + name);
    }

    private static void Equal<T>(T expected, T actual, string message)
    {
        if (!object.Equals(expected, actual))
            throw new Exception(message + ": expected " + expected + ", got " + actual);
    }

    private static void InitialState()
    {
        var state = new PetState(15, Start);
        Equal(PetMode.Cooling, state.Mode, "mode");
        Equal(0, state.StrikeCount, "strikes");
        Equal(1, state.AngerLevel, "anger");
        Equal(Start.AddMinutes(15), state.NextDueUtc, "deadline");
        Equal(TimeSpan.FromMinutes(15), state.Remaining(Start), "remaining");
        Equal(1, new PetState(-3, Start).IntervalMinutes, "lower clamp");
        Equal(180, new PetState(200, Start).IntervalMinutes, "upper clamp");
    }

    private static void Deadline()
    {
        var state = new PetState(15, Start);
        Equal(false, state.Tick(Start.AddMinutes(15).AddTicks(-1)), "before deadline");
        Equal(true, state.Tick(Start.AddMinutes(15)), "at deadline");
        Equal(PetMode.Whipping, state.Mode, "whipping mode");
        Equal(1, state.StrikeCount, "first strike");
        Equal(TimeSpan.Zero, state.Remaining(Start.AddMinutes(15)), "whipping remaining");
        Equal(false, state.Tick(Start.AddMinutes(15)), "duplicate tick");
    }

    private static void StrikeEscalation()
    {
        var state = new PetState(15, Start);
        state.TestWhip(Start);
        DateTime now = Start;
        double[] delays = { 3.2, 2.2, 1.3, 1.1, 0.95, 0.8 };
        for (int count = 1; count <= 24; count++)
        {
            Equal(true, state.Tick(now), "strike " + count);
            Equal(count, state.StrikeCount, "strike count");
            int anger = Math.Min(6, (count + 2) / 3);
            Equal(anger, state.AngerLevel, "anger " + count);
            double delay = delays[anger - 1];
            DateTime next = now.AddSeconds(delay);
            Equal(false, state.Tick(next.AddTicks(-1)), "before next strike");
            now = next;
        }
    }

    private static void LargeTimeJump()
    {
        var state = new PetState(15, Start);
        DateTime later = Start.AddDays(1);
        Equal(true, state.Tick(later), "overdue strike");
        Equal(1, state.StrikeCount, "no catchup burst");
        Equal(false, state.Tick(later), "same instant");
        Equal(true, state.Tick(later.AddDays(1)), "second long jump");
        Equal(2, state.StrikeCount, "still one strike");
    }

    private static void CalmReset()
    {
        var state = new PetState(15, Start);
        state.TestWhip(Start);
        state.Tick(Start);
        DateTime now = Start.AddMinutes(2);
        state.Calm(now);
        Equal(PetMode.Cooling, state.Mode, "calm mode");
        Equal(0, state.StrikeCount, "calm strikes");
        Equal(now.AddMinutes(15), state.NextDueUtc, "fresh deadline");
        Equal(false, state.Tick(now), "calm no strike");
    }

    private static void CoolingInterval()
    {
        var state = new PetState(15, Start);
        DateTime now = Start.AddMinutes(4);
        state.SetInterval(5, now);
        Equal(Start.AddMinutes(9), state.NextDueUtc, "interval restart");
        state.SetInterval(0, now);
        Equal(1, state.IntervalMinutes, "clamped change");
        Equal(now.AddMinutes(1), state.NextDueUtc, "clamped deadline");
    }

    private static void WhippingInterval()
    {
        var state = new PetState(15, Start);
        state.TestWhip(Start);
        state.Tick(Start);
        state.SetInterval(5, Start.AddSeconds(1));
        Equal(PetMode.Whipping, state.Mode, "still whipping");
        Equal(1, state.StrikeCount, "preserved strikes");
        Equal(false, state.Tick(Start.AddSeconds(3.2).AddTicks(-1)), "preserved schedule before");
        Equal(true, state.Tick(Start.AddSeconds(3.2)), "preserved schedule due");
        state.Calm(Start.AddSeconds(4));
        Equal(TimeSpan.FromMinutes(5), state.Remaining(Start.AddSeconds(4)), "new interval on calm");
    }

    private static void ManualWhip()
    {
        var state = new PetState(15, Start);
        state.TestWhip(Start.AddMinutes(1));
        Equal(PetMode.Whipping, state.Mode, "manual mode");
        Equal(0, state.StrikeCount, "no immediate emitted strike");
        Equal(true, state.Tick(Start.AddMinutes(1)), "next tick strike");
        state.TestWhip(Start.AddMinutes(2));
        Equal(0, state.StrikeCount, "manual reset");
        Equal(PetMode.Whipping, state.Mode, "repeated test remains whipping");
        Equal(true, state.Tick(Start.AddMinutes(2)), "repeated test emits next tick");
        Equal(1, state.StrikeCount, "repeated test resets escalation");
        Equal(false, state.Tick(Start.AddMinutes(2)), "repeated test does not duplicate instant strike");
    }

    private static void DefaultStages()
    {
        var state = new PetState(15, Start);
        Equal(0, state.CountdownStage(Start), "initial stage");
        Equal(0, state.CountdownStage(Start.AddMinutes(8).AddTicks(-1)), "before stage one");
        Equal(1, state.CountdownStage(Start.AddMinutes(8)), "stage one boundary");
        Equal(1, state.CountdownStage(Start.AddMinutes(12).AddTicks(-1)), "before stage two");
        Equal(2, state.CountdownStage(Start.AddMinutes(12)), "stage two boundary");
        Equal(2, state.CountdownStage(Start.AddMinutes(14).AddTicks(-1)), "before stage three");
        Equal(3, state.CountdownStage(Start.AddMinutes(14)), "stage three boundary");
        Equal(3, state.CountdownStage(Start.AddDays(1)), "overdue before tick");
    }

    private static void CustomStages()
    {
        var state = new PetState(3, Start);
        DateTime stageOne = Start.AddSeconds(96);
        DateTime stageTwo = Start.AddSeconds(144);
        DateTime stageThree = Start.AddSeconds(168);
        Equal(0, state.CountdownStage(stageOne.AddTicks(-1)), "custom before stage one");
        Equal(1, state.CountdownStage(stageOne), "custom stage one");
        Equal(1, state.CountdownStage(stageTwo.AddTicks(-1)), "custom before stage two");
        Equal(2, state.CountdownStage(stageTwo), "custom stage two");
        Equal(2, state.CountdownStage(stageThree.AddTicks(-1)), "custom before stage three");
        Equal(3, state.CountdownStage(stageThree), "custom stage three");
    }

    private static void ModeStages()
    {
        var state = new PetState(15, Start);
        state.TestWhip(Start.AddDays(1));
        Equal(4, state.CountdownStage(Start.AddDays(1)), "whipping stage");
        state.Calm(Start.AddDays(1));
        Equal(0, state.CountdownStage(Start.AddDays(1)), "calm returns initial stage");
    }

    private static void RepeatedCalm()
    {
        var state = new PetState(15, Start);
        state.Calm(Start.AddMinutes(10));
        Equal(Start.AddMinutes(25), state.NextDueUtc, "calm while cooling restarts interval");
        Equal(false, state.Tick(Start.AddMinutes(15)), "old deadline no longer triggers");
        Equal(true, state.Tick(Start.AddMinutes(25)), "new deadline triggers");
        state.Calm(Start.AddMinutes(26));
        Equal(PetMode.Cooling, state.Mode, "calm after whipping returns cooling");
        Equal(0, state.StrikeCount, "calm clears prior strikes");
        Equal(Start.AddMinutes(41), state.NextDueUtc, "next complete cooldown");
    }

    private static void AcrossMidnight()
    {
        DateTime late = new DateTime(2026, 10, 9, 23, 58, 0, DateTimeKind.Utc);
        var state = new PetState(15, late);
        Equal(PetMode.Cooling, state.Mode, "late hours still start cooling");
        Equal(false, state.Tick(late.AddMinutes(15).AddTicks(-1)), "midnight does not skip cooldown");
        Equal(true, state.Tick(late.AddMinutes(15)), "deadline after midnight starts whipping");
        Equal(PetMode.Whipping, state.Mode, "whipping after midnight");
        Equal(true, state.Tick(late.AddDays(1)), "whipping continues next day");
        Equal(PetMode.Whipping, state.Mode, "no time-of-day resting state");
        Equal(2, state.StrikeCount, "long jump emits a single additional strike");
    }

    private static void BeijingEvening()
    {
        DateTime evening = new DateTime(2026, 10, 9, 10, 5, 0, DateTimeKind.Utc); // Beijing 18:05.
        var state = new PetState(15, evening);
        DateTime due = new DateTime(2026, 10, 9, 10, 20, 0, DateTimeKind.Utc);
        Equal(false, state.Tick(due.AddTicks(-1)), "evening before deadline");
        Equal(true, state.Tick(due), "Beijing 18:20 still starts whipping");
        Equal(PetMode.Whipping, state.Mode, "evening remains active");
        Equal(true, state.Tick(due.AddSeconds(3.2)), "evening continues whipping");
        state.Calm(due.AddMinutes(1));
        Equal(true, state.Tick(due.AddMinutes(16)), "evening calm starts another full cycle");
    }

    private static void BeijingMidnight()
    {
        DateTime late = new DateTime(2026, 10, 9, 15, 50, 0, DateTimeKind.Utc); // Beijing 23:50.
        DateTime midnight = new DateTime(2026, 10, 9, 16, 0, 0, DateTimeKind.Utc);
        DateTime due = new DateTime(2026, 10, 9, 16, 5, 0, DateTimeKind.Utc);
        var state = new PetState(15, late);
        Equal(false, state.Tick(midnight), "Beijing date change does not emit early");
        Equal(PetMode.Cooling, state.Mode, "Beijing midnight remains cooling");
        Equal(due, state.NextDueUtc, "Beijing midnight preserves original deadline");
        Equal(TimeSpan.FromMinutes(5), state.Remaining(midnight), "Beijing midnight keeps remaining time");
        Equal(false, state.Tick(due.AddTicks(-1)), "before next Beijing day's deadline");
        Equal(true, state.Tick(due), "Beijing 00:05 starts whipping");
        Equal(true, state.Tick(due.AddSeconds(3.2)), "whipping continues after Beijing midnight");
    }

    private static void MaximumRageInterval()
    {
        var state = new PetState(15, Start);
        state.TestWhip(Start);
        DateTime now = Start;
        for (int count = 1; count <= 16; count++)
        {
            state.Tick(now);
            if (count < 16) now = now.AddSeconds(4);
        }
        Equal(6, state.AngerLevel, "sixth level starts at strike sixteen");
        state.SetInterval(300, now);
        Equal(180, state.IntervalMinutes, "upper interval clamp");
        Equal(PetMode.Whipping, state.Mode, "interval change keeps whipping");
        Equal(16, state.StrikeCount, "interval change keeps strike count");
        Equal(6, state.AngerLevel, "interval change keeps maximum rage");
        Equal(false, state.Tick(now.AddSeconds(0.8).AddTicks(-1)), "preserved maximum rage delay");
        Equal(true, state.Tick(now.AddSeconds(0.8)), "next maximum rage strike");
        state.Calm(now.AddSeconds(1));
        Equal(TimeSpan.FromMinutes(180), state.Remaining(now.AddSeconds(1)), "new interval used when calmed");
    }
}
