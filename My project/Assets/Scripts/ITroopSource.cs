// Anything that can pay for an advance: a nation's home reserve, or the finite
// force committed to a single order. Keeping this behind an interface lets
// MapData charge either one without knowing which it holds.
public interface ITroopSource
{
    int Id { get; }
    int Manpower { get; }
    bool TrySpendManpower(int amount);
}
