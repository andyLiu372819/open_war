# Large-map verification — 7 September 2026

Default scene and code dimensions: 1,024 × 512 = 524,288 cells, including water (12.8 times the previous map).

Passed the existing simulation suite and new large-map checks. These cover ownership transfers and cached counts, elimination of empty labels, a full long-distance advance, and unchanged factions retaining their label cache. A complete cell scan after combat agreed with every cached territory count.

Observed timings in the standalone .NET 8 test process on this machine:

| Operation | Time |
| --- | ---: |
| Generate 524,288 cells | 593 ms |
| Place four connected starts | 276 ms |
| Find a 1,053-cell route to a rival | 177 ms |
| Simulate the entire funded advance | 21 ms |
| 4,000 territory count/center queries after the advance | 199 ms |

These are simulation benchmarks, not Unity frame-rate measurements. Generation and route searches still execute synchronously.

Unity 6000.5.4f1 compiled the scripts and passed the isolated scene check recorded in `large-map-smoke.log` (`SCENE_SMOKE_PASS`). Checks included:

- Correct scene dimensions and 128 × 64 world-unit bounds.
- 32 render sections, each containing up to 128 × 128 simulation cells.
- A corner-seam refresh uploaded exactly three affected textures; the next unchanged frame uploaded none.
- A 259 × 131 test map rendered six sections, including the partial 3 × 3 corner; refreshing that corner uploaded only one section.
- Screen-to-cell conversion on a translated, scaled, rotated map.
- Numeric territory labels, left-click orders, timed expansion, fast wheel zoom, middle-drag panning, and right-click cancellation.

Inspected `large-overview.png` for terrain continuity. `large-advance.png` records the test advance with extra test manpower. Unity again logged its editor SearchDatabase indexing exception, excluded from gameplay error tracking as documented in `RESULTS.md`; no gameplay exceptions occurred.

Temporary Unity Library/Temp/Logs caches were removed after the run. The minimal isolated test project uses the existing Input System and uGUI packages. The game's own package manifest was not changed.
