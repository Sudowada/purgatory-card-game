using System;

public static class RadioBehaviorChecks
{
    static int checks;
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        checks++;
    }
    public static void Main()
    {
        var radio = new RadioStationCode();
        for (int i = 0; i < 26; i++)
        {
            if (i == 13) radio.ToggleBand();
            radio.Tune(i % 13);
            Check(radio.Frequency == 440 + i * 10, "Frequency for letter " + i);
            Check(radio.Letter == 'A' + i, "Alphabet mapping " + i);
        }
        radio.Tune(-1); Check(radio.Station == 0, "Lower clamp");
        radio.Tune(99); Check(radio.Station == 12, "Upper clamp");
        radio.ToggleBand(); Check(radio.Station == 12 && radio.Frequency == 560, "Band preserves dial position");
        radio = new RadioStationCode();
        radio.ToggleBand(); radio.Tune(1);
        Check(radio.Frequency == 580 && !radio.Confirm(), "O FM 580");
        radio.Tune(0); Check(radio.Frequency == 570 && !radio.Confirm(), "N FM 570");
        radio.ToggleBand(); radio.Tune(4);
        Check(radio.Frequency == 480 && radio.Confirm() && radio.Solved, "E AM 480 solves ONE");
        Check(!radio.Confirm(), "Reward only once");
        radio.Clear(); radio.Backspace(); Check(radio.Entry == "ONE", "Solved state stays solved");
        radio = new RadioStationCode();
        radio.Confirm(); radio.Confirm(); radio.Confirm();
        Check(!radio.Solved && radio.Entry == "AAA", "Wrong code rejected");
        radio.ToggleBand(); radio.Tune(1); radio.Confirm();
        Check(radio.Entry == "O", "Next attempt replaces wrong code");
        radio.Backspace(); Check(radio.Entry == "", "Undo");
        radio.Backspace(); Check(radio.Entry == "", "Empty undo");
        radio.Confirm(); radio.Clear(); Check(radio.Entry == "", "Clear");
        Check(new RadioStationCode(" one ").Code == "ONE", "Normalize Inspector code");
        foreach (string invalid in new[] { "", "777", "A B", "ABCDEFGHIJKLM" })
        {
            bool rejected = false;
            try { new RadioStationCode(invalid); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Reject invalid code");
        }
        var dial = new RadioDialMotion(0f, 12, 0.0015f);
        dial.Move(20f); Check(dial.Detent == 0, "Small movement stays in station detent");
        dial.Move(10f); Check(dial.Detent == 1, "Small pointer motions accumulate");
        dial.Move(0f); Check(dial.Detent == 1, "No drift at rest");
        dial.Move(-10f); Check(dial.Detent == 0, "Reverse direction returns to previous station");
        dial.Move(10000f); Check(dial.Detent == 12, "Drag clamps at upper end");
        dial.Move(-30f); Check(dial.Detent == 11, "Reverse immediately after hitting upper limit");
        dial.Move(-10000f); Check(dial.Detent == 0, "Drag clamps at lower end");
        dial.Move(30f); Check(dial.Detent == 1, "Reverse immediately after lower limit");
        dial = new RadioDialMotion(0.5f, 50, 0.0015f);
        Check(dial.Value == 0.5f, "Grabbing volume does not move it");
        dial.Move(14f); Check(Math.Abs(dial.Value - 0.52f) < 0.0001f, "Volume clicks in two-percent steps");
        dial.Move(-14f); Check(dial.Value == 0.5f, "Drag reversal has no inertia");
        dial = new RadioDialMotion(0f, 12, 0.0015f);
        dial.Move(250f); Check(dial.Value < 0.5f, "Precision drag is slower than old full-range 250px drag");
        Console.WriteLine(checks + " radio behavior checks passed.");
    }
}
