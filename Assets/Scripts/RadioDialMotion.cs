using System;

/// <summary>Accumulates slow pointer travel without losing sub-detent precision.</summary>
public sealed class RadioDialMotion
{
    readonly int steps;
    readonly float sensitivity;
    float accumulated;
    public int Detent => (int)Math.Round(accumulated * steps, MidpointRounding.ToEven);
    public float Value => Detent / (float)steps;
    public RadioDialMotion(float startValue, int steps, float sensitivity)
    {
        this.steps = Math.Max(1, steps);
        this.sensitivity = Math.Max(0.0001f, sensitivity);
        accumulated = Math.Max(0f, Math.Min(1f, startValue));
    }
    public void Move(float pixels)
    {
        accumulated = Math.Max(0f, Math.Min(1f, accumulated + pixels * sensitivity));
    }
}
