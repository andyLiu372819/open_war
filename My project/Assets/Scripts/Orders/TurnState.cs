// WeGo turn bookkeeping. Orders are given while the turn is being planned and
// nothing on the map moves; when the turn is executed every unit resolves its
// orders together over a fixed number of simultaneous steps.
public enum TurnPhase { Planning, Executing }

public sealed class TurnState
{
    // How many simultaneous steps one turn of execution is worth.
    public const int StepsPerTurn = 14;

    public int Turn { get; private set; } = 1;
    public TurnPhase Phase { get; private set; } = TurnPhase.Planning;
    public int StepsRemaining { get; private set; }
    public bool IsPlanning => Phase == TurnPhase.Planning;
    public bool IsExecuting => Phase == TurnPhase.Executing;
    // 0 at the start of execution, 1 when the turn has fully resolved.
    public float Progress => StepsRemaining <= 0 ? 1f :
        1f - StepsRemaining / (float)StepsPerTurn;

    // Locks in the orders given during planning and starts resolving them.
    public bool BeginExecution()
    {
        if (Phase != TurnPhase.Planning) return false;
        Phase = TurnPhase.Executing;
        StepsRemaining = StepsPerTurn;
        return true;
    }

    // Consumes one simultaneous step. False once the turn is spent.
    public bool TryStep()
    {
        if (Phase != TurnPhase.Executing || StepsRemaining <= 0) return false;
        StepsRemaining--;
        return true;
    }

    // Closes a fully resolved turn and opens planning for the next one.
    public bool TryComplete()
    {
        if (Phase != TurnPhase.Executing || StepsRemaining > 0) return false;
        Phase = TurnPhase.Planning;
        Turn++;
        return true;
    }
}
