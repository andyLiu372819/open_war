using System;

// Troops detached from a nation's reserve and handed to one advance. The order
// spends from this pool alone, so an attack is bounded by what was committed to
// it rather than by everything the nation owns. Survivors are recalled when the
// order ends.
public sealed class CommittedForce : ITroopSource
{
    public int Id { get; }
    public int Committed { get; }
    public int Manpower { get; private set; }
    public int Refunded { get; private set; }
    public int Spent => Committed - Manpower - Refunded;

    public CommittedForce(int ownerId, int troops)
    {
        if (ownerId < 0)
            throw new ArgumentOutOfRangeException(nameof(ownerId));
        if (troops < 0)
            throw new ArgumentOutOfRangeException(nameof(troops));
        Id = ownerId;
        Committed = troops;
        Manpower = troops;
    }

    public bool TrySpendManpower(int amount)
    {
        // Reject non-positive amounts or more than the force still carries.
        if (amount > 0 && Manpower - amount >= 0)
        {
            Manpower -= amount;
            return true;
        }
        return false;
    }

    // Empties the pool and reports what is left for the nation to take back.
    public int Recall()
    {
        int remaining = Manpower;
        Manpower = 0;
        Refunded += remaining;
        return remaining;
    }
}
