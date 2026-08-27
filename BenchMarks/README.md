# BenchMarks

[BenchmarkDotNet](https://benchmarkdotnet.org/) suite for the spatial trees. Use it to
measure the effect of a perf change instead of guessing.

## Running

Always run in **Release** - BenchmarkDotNet refuses a Debug build.

```
# interactive menu of every benchmark class
dotnet run -c Release --project BenchMarks

# one class / one method (glob filter)
dotnet run -c Release --project BenchMarks -- --filter *Quadtree*
dotnet run -c Release --project BenchMarks -- --filter *QueryRectangle*

# quick, low-precision pass while iterating on code
dotnet run -c Release --project BenchMarks -- --filter *Quadtree* --job short

# just list what exists
dotnet run -c Release --project BenchMarks -- --list flat
```

Results (tables + full logs) are written under `BenchmarkDotNet.Artifacts/`, which is
git-ignored. A full default run of everything takes a few minutes; use `--job short`
or a tight `--filter` while developing.

## What's covered

| Class | Kind | Benchmarks |
|-------|------|-----------|
| `QuadtreeBenchmarks` / `OctreeBenchmarks` | non-destructive, `[Params]` 1k/10k/50k items | `Build` (bulk insert), `QueryRectangle`/`QueryCube`, `QueryCircle`/`QuerySphere` (1000 small localised queries) |
| `QuadtreeMutationBenchmarks` / `OctreeMutationBenchmarks` | destructive, 50k items, rebuilt each iteration | `RemoveAll`, `MoveAll` |

All classes use `[MemoryDiagnoser]` - the **Allocated** column is the number we care
about most right now.

## Conventions

- `WorldData` generates the world bounds, item positions and query volumes from a fixed
  seed, so every run is comparable to the last.
- `BenchItem2d` / `BenchItem3d` rebuild their `BoundingBox` on every access, matching the
  common caller pattern - that allocation is part of what we're measuring.
- Tree tuning (`MaxDepth = 8`, `MaxObjects = 16`) is fixed in the benchmark classes so
  item count is the only variable. Change those consts to explore the split heuristic.
- The mutation benchmarks run under `RunStrategy.Monitoring` (one invocation per
  iteration) because their bodies mutate shared state; their numbers are noisier than the
  throughput benchmarks - compare deltas, not absolutes.

## Workflow for a perf change

1. Run the relevant filter on `master`, save the summary table.
2. Make the change.
3. Re-run the same filter, diff the `Mean` and `Allocated` columns.
