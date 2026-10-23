using System;

public enum RadioBand { AM, FM }

/// <summary>Fictional puzzle frequencies, independent of audio pitch and Unity UI.</summary>
public sealed class RadioStationCode
{
    public const int StationsPerBand = 13;
    public const int FirstFrequency = 440;
    public const int FrequencyStep = 10;
    public RadioBand Band { get; private set; }
    public int Station { get; private set; }
    public int Frequency => FirstFrequency + FrequencyStep * ((int)Band * StationsPerBand + Station);
    public char Letter => (char)('A' + (int)Band * StationsPerBand + Station);
    public string Entry { get; private set; } = "";
    public string Code { get; }
    public bool Solved { get; private set; }

    public RadioStationCode(string code = "ONE")
    {
        Code = (code ?? "").Trim().ToUpperInvariant();
        if (Code.Length == 0 || Code.Length > 12) throw new ArgumentException("Use 1-12 letters.");
        foreach (char letter in Code)
            if (letter < 'A' || letter > 'Z') throw new ArgumentException("Use letters A-Z only.");
    }

    public void Tune(int station) => Station = Math.Max(0, Math.Min(StationsPerBand - 1, station));
    public void ToggleBand() => Band = Band == RadioBand.AM ? RadioBand.FM : RadioBand.AM;

    // Returns true once for the successful final selection. A wrong full entry
    // stays visible until the next selection, which starts a fresh attempt.
    public bool Confirm()
    {
        if (Solved) return false;
        if (Entry.Length >= Code.Length) Entry = "";
        Entry += Letter;
        if (Entry != Code) return false;
        Solved = true;
        return true;
    }

    public void Clear() { if (!Solved) Entry = ""; }
    public void Backspace()
    {
        if (!Solved && Entry.Length > 0) Entry = Entry.Substring(0, Entry.Length - 1);
    }
}
