using System;

public enum Phase { Focus, ShortBreak, LongBreak }

public sealed class TimerEngine
{
    public Phase Phase { get; private set; }
    public bool Running { get; private set; }
    public int Completed { get; private set; }
    public double Remaining { get; private set; }
    private DateTime deadline;
    public static int Duration(Phase phase) { return phase == Phase.Focus ? 1500 : phase == Phase.ShortBreak ? 300 : 900; }
    public TimerEngine() { Select(Phase.Focus); }
    public void Select(Phase phase) { Phase = phase; Running = false; Remaining = Duration(phase); }
    public void Start(DateTime now) { if (!Running) { deadline = now.AddSeconds(Remaining); Running = true; } }
    public void Pause(DateTime now) { if (Running) { Remaining = Math.Max(0, (deadline - now).TotalSeconds); Running = false; } }
    public void Reset() { Select(Phase); }
    public bool Tick(DateTime now, bool autoStart)
    {
        if (!Running) return false;
        Remaining = Math.Max(0, (deadline - now).TotalSeconds);
        if (Remaining > 0) return false;
        if (Phase == Phase.Focus) { Completed++; Select(Completed % 4 == 0 ? Phase.LongBreak : Phase.ShortBreak); }
        else Select(Phase.Focus);
        if (autoStart) Start(now);
        return true;
    }
}
