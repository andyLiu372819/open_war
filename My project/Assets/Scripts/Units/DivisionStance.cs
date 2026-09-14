// What a division is trying to do this turn. The stance decides how it behaves
// when the turn resolves, not merely how it is drawn.
public enum DivisionStance
{
    // Advance on the ordered destination, taking wilderness and fighting for
    // contested ground. Costs strength wherever it meets resistance.
    Attack,
    // Move through friendly ground to an assigned held line, then dig in. With
    // no line assigned, hold the current position and raise its defence.
    Defend,
    // Held back out of the line. Does not move or fight, and absorbs
    // replacements from the national military population to rebuild strength.
    Reserve,
    // Reposition without fighting. Moves faster than an attack, but refuses to
    // enter contested ground and stops short of it instead.
    Redeploy
}
