# Encirclement verification — 8 September 2026

Passed 91 targeted assertions plus 200,443 existing simulation assertions using the Unity-installed .NET 8 SDK. Scripts also compile against the actual Unity Assembly-CSharp references using its Roslyn compiler.

Targeted scenarios cover:

- Actual wilderness and enemy spending on every walkable terrain, unaffordable actions, and mountain garrison resistance.
- Open versus closed rings, faction-specific 50% discounts, mixed-faction rings, water openings, and map-edge openings.
- No free ownership transfer during detection; automatically funded waves, full pocket capture, multiple pockets, exhaustion, later recruitment, and exactly-once refunds.
- Immediate invalidation on a breached ring, including refunding its active automatic assault.
- Concurrent manual and automatic captures without duplicate charges or stranded fronts.
- Manual advances continuing past the clicked tile to the map edge, stopping at water, and preserving spending totals after recall.
- Detection of 149,201 enclosed cells on a 524,288-cell map; unchanged enclosures skip subsequent flood scans.

The last standalone run observed 72 ms for the large enclosure scan, 743 ms for full map generation, 274 ms for start placement, and 384 ms for a long-distance order with three fronts. These are simulation timings on this machine, not Unity frame-rate guarantees.

Unity 6000.5.4f1 passed the isolated `SceneSmoke` harness (`SCENE_SMOKE_PASS` in `encirclement-smoke.log`). It verified the large map and chunk updates, numeric territory labels, mouse orders, timed expansion, zoom, dragging, commitment-panel input blocking, redirecting a 100%-committed force, and exact recall accounting.

The harness then surrounded 25 neutral/enemy cells on the generated map, checked the discounted hover readout, and let the real MapController update loop capture the entire pocket without a manual order. Inspected `encirclement-before.png` and `encirclement-after.png` to verify terrain, ownership changes, and HUD placement. `encirclement-overview.png` and `encirclement-advance.png` record the earlier navigation/advance checks. Test reserves were increased to keep those checks independent of recruitment.

No gameplay errors were logged. The fresh isolated editor again emitted the unrelated UnityEditor.Search.SearchDatabase indexing exception documented in earlier results; that editor stack is excluded from the gameplay error flag. The game's package manifest and editor session were not changed. Temporary isolated Unity caches were removed after testing.

The existing project scripts, scene, and README before this work are in `../Backups/before-encirclement.zip`. Gameplay rules and future division/supply integration boundaries are documented in `../My project/README.md`.
