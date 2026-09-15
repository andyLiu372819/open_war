public class PlayerData : ITroopSource
{
    public int Id { get; }
    // Military population. This is the only pool combat can spend, and it never
    // grows on its own: it is recruited out of civilians and paid for in coins.
    public int Manpower { get; private set; }
    // Civilian population. Grows every second with owned land and is the pool
    // recruits are drawn from, so land feeds people and people feed the army.
    public decimal Civilians { get; private set; }
    public decimal Gold { get; private set; }
    public decimal Industry { get; private set; }

    public const decimal CivilianBase = 30m;
    // Per-cell rates suit nations holding tens of thousands of cells. The
    // simulation applies these once per simulated second.
    public const decimal CiviliansPerCell = 0.08m;
    public const decimal GoldPerCell = 0.06m;
    public const decimal IndustryPerCell = 0.03m;
    // One soldier costs one civilian and one coin. Because coins accrue at half
    // a cell per second and civilians at half a cell plus a flat base, a small
    // nation is limited by its treasury and a large one by both at once, so
    // neither number is ever decorative.
    public const decimal CiviliansPerRecruit = 1m;
    public const decimal GoldPerRecruit = 1m;
    public const decimal StartingCivilians = 40000m;

    public static decimal CiviliansPerSecond(int land) =>
        land > 0 ? CivilianBase + land * CiviliansPerCell : 0m;
    public static decimal GoldPerSecond(int land) => System.Math.Max(0, land) * GoldPerCell;
    public static decimal IndustryPerSecond(int land) => System.Math.Max(0, land) * IndustryPerCell;

    // Called once per simulation second with the current owned-cell count.
    // Decimal balances preserve fractional production, even for a single tile.
    public void TickEconomy(int ownedCells)
    {
        if (ownedCells < 0) throw new System.ArgumentOutOfRangeException(nameof(ownedCells));
        if (ownedCells == 0) return;
        Civilians += CiviliansPerSecond(ownedCells);
        Gold += GoldPerSecond(ownedCells);
        Industry += IndustryPerSecond(ownedCells);
    }

    // Soldiers the civilian pool and the treasury can pay for together.
    public int AffordableRecruits()
    {
        decimal byPeople = System.Math.Floor(Civilians / CiviliansPerRecruit);
        decimal byCoin = System.Math.Floor(Gold / GoldPerRecruit);
        decimal fewest = System.Math.Min(byPeople, byCoin);
        if (fewest <= 0m) return 0;
        return fewest > int.MaxValue ? int.MaxValue : (int)fewest;
    }

    // Recruitment is all-or-nothing: a partially paid batch would leave the
    // civilian and coin ledgers disagreeing about how many soldiers exist.
    public bool TryRecruit(int troops)
    {
        if (troops <= 0) return false;
        decimal people = troops * CiviliansPerRecruit;
        decimal coin = troops * GoldPerRecruit;
        if (Civilians < people || Gold < coin) return false;
        Civilians -= people;
        Gold -= coin;
        Manpower += troops;
        return true;
    }

    public PlayerData(int id, int startingManpower, decimal startingCivilians = 0m)
    {
        // Reject negative arguments, then initialize the properties.
        if (id >= 0 && startingManpower >= 0 && startingCivilians >= 0m)
        {
            Id = id;
            Manpower = startingManpower;
            Civilians = startingCivilians;
        }
        else
        {
            if (id < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(id));
            }
            else if (startingManpower < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(startingManpower));
            }
            else
            {
                throw new System.ArgumentOutOfRangeException(nameof(startingCivilians));
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
