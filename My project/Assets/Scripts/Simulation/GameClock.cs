using System;

// Converts wall-clock time into fixed simulation ticks. Unity supplies elapsed
// real time, but gameplay only observes ticks consumed from this clock.
public sealed class GameClock
{
    public const double DefaultFixedTickDuration = 0.12d;
    private const double TickEpsilon = 0.000000001d;

    private double accumulator;

    public double FixedTickDuration { get; }
    public bool Paused { get; private set; }
    public int SpeedMultiplier { get; private set; } = 1;
    public long TickCount { get; private set; }
    public double SimulationTime => TickCount * FixedTickDuration;

    public GameClock(double fixedTickDuration = DefaultFixedTickDuration)
    {
        if (double.IsNaN(fixedTickDuration) || double.IsInfinity(fixedTickDuration) ||
            fixedTickDuration <= 0d)
            throw new ArgumentOutOfRangeException(nameof(fixedTickDuration));
        FixedTickDuration = fixedTickDuration;
    }

    public void AddRealTime(double deltaTime)
    {
        if (double.IsNaN(deltaTime) || double.IsInfinity(deltaTime) || deltaTime < 0d)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        if (!Paused) accumulator += deltaTime * SpeedMultiplier;
    }

    public bool TryConsumeTick()
    {
        if (Paused || accumulator + TickEpsilon < FixedTickDuration) return false;
        accumulator -= FixedTickDuration;
        if (accumulator < 0d) accumulator = 0d;
        TickCount++;
        return true;
    }

    public void SetPaused(bool paused) => Paused = paused;

    public bool TogglePaused()
    {
        Paused = !Paused;
        return Paused;
    }

    public bool SetSpeed(int multiplier)
    {
        if (multiplier != 1 && multiplier != 2 && multiplier != 4) return false;
        SpeedMultiplier = multiplier;
        return true;
    }
}
