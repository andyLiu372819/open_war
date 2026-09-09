public class PlayerData : ITroopSource
{
    public int Id { get; }
    public int Manpower { get; private set; }
    public decimal Gold { get; private set; }
    public decimal Industry { get; private set; }

    public const int BaseRecruitment = 140;
    public const int RecruitmentPerCell = 1;
    public const decimal GoldPerCell = 0.5m;
    public const decimal IndustryPerCell = 0.25m;

    public static int RecruitmentPerSecond(int land) => land > 0 ? BaseRecruitment + land * RecruitmentPerCell : 0;
    public static decimal GoldPerSecond(int land) => System.Math.Max(0, land) * GoldPerCell;
    public static decimal IndustryPerSecond(int land) => System.Math.Max(0, land) * IndustryPerCell;

    // Called once per simulation second with the current owned-cell count.
    // Decimal balances preserve fractional production, even for a single tile.
    public void TickEconomy(int ownedCells)
    {
        if (ownedCells < 0) throw new System.ArgumentOutOfRangeException(nameof(ownedCells));
        if (ownedCells == 0) return;
        AddManpower(RecruitmentPerSecond(ownedCells));
        Gold += GoldPerSecond(ownedCells);
        Industry += IndustryPerSecond(ownedCells);
    }

    public PlayerData(int id, int startingManpower)
    {
        // Reject negative arguments, then initialize the properties.
        if (id >= 0 && startingManpower >= 0)
        {
            Id = id;
            Manpower = startingManpower;
        }
        else
        {
            if (id < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(id));
            }
            else
            {
                throw new System.ArgumentOutOfRangeException(nameof(startingManpower));
            }
        }
    }

    public void AddManpower(int amount)
    {
        // Reject negative amounts, then add to Manpower.
        if (amount >= 0)
        {
            Manpower += amount;
        }

        else
        {
            throw new System.ArgumentOutOfRangeException(nameof(amount));
        }
    }

    public bool TrySpendManpower(int amount)
    {
        // Reject non-positive amounts or insufficient manpower.
        // Otherwise deduct the amount and return true.
        if (amount > 0 && Manpower - amount >= 0)
        {
            Manpower -= amount;
            return true;
        }
        else
        {
            return false;
        }
    }
}
