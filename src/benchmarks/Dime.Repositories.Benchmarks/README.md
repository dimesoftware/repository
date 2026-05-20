# Dime.Repositories.Benchmarks

BenchmarkDotNet harness for the `splitQuery: bool` opt-in added in `3.2.0-beta.3` on
`FindAllPagedAsync` (and `FindAllAsync<TResult>`).

## What this measures

Six benchmarks comparing `splitQuery: false` (single SQL JOIN with Cartesian fan-out) vs
`splitQuery: true` (separate `SELECT` per collection nav) on a seeded fixture:

| Scenario | Page | Includes |
| -------- | ---- | -------- |
| `Page1Of50_*` | 1 of 50 | `Posts` + `Tags` |
| `Page1Of10_*` | 1 of 10 | `Posts` + `Tags` |
| `FullScan_*` | all 1,000 Blogs | `Posts` + `Tags` |

Fixture seed (in `[GlobalSetup]`, reused across iterations):

- **1,000 Blogs**
- **10 Posts per Blog** (10,000 Posts total)
- **15 Tags per Blog** (15,000 Tags total)
- Cartesian fan-out when both collections are joined: **150 join rows per Blog** (150,000 for the full set)

`[MemoryDiagnoser]` is on — allocations matter at least as much as wall time for this comparison.

## Running

From the repo root:

```powershell
dotnet run -c Release --project src/benchmarks/Dime.Repositories.Benchmarks
```

Filter to a subset:

```powershell
dotnet run -c Release --project src/benchmarks/Dime.Repositories.Benchmarks -- --filter '*Page1Of50*'
```

List benchmarks without running:

```powershell
dotnet run -c Release --project src/benchmarks/Dime.Repositories.Benchmarks -- --list flat
```

Results are written to `BenchmarkDotNet.Artifacts/results/` (markdown + CSV + log).

## Honesty: SQLite is not SQL Server

The benchmark uses file-based SQLite. This is **not** representative of SQL Server's wire
protocol, query planner, or join algorithms. What you can reasonably draw from these numbers:

- **Relative deltas** between split and single mode on identical seeds: indicative of the
  materialization / traversal / allocation cost EF Core itself pays for the Cartesian product.
  This part of the cost is provider-agnostic.
- **Absolute timings, throughput, or SQL Server-specific cost** (logical reads, tempdb
  spills, parameter sniffing, plan cache effects): **do not infer**. Use a real SQL Server.

If a clear delta does not show up at this seed size on SQLite, scale `BlogCount`,
`PostsPerBlog`, `TagsPerBlog` in `BenchmarkFixture.cs` and re-run.

## Pointing at a real SQL Server

In `BenchmarkFixture.cs`, swap the options builder:

```csharp
Options = new DbContextOptionsBuilder<BloggingContext>()
    .UseSqlServer("Server=...;Database=...;...")  // was: .UseSqlite(ConnectionString)
    .Options;
```

Notes when you do this:

- Drop the `Dispose()` SQLite-specific file cleanup (or guard it).
- Increase `BlogCount` / `PostsPerBlog` / `TagsPerBlog` — SQL Server's planner is happy
  to JOIN tiny tables; you want enough rows that the fan-out actually hurts (try 10,000
  Blogs × 20 Posts × 25 Tags as a starting point).
- Consider adding `MultipleActiveResultSets=True` and a warm-up benchmark that runs
  outside the measured section so plan cache / first-call costs are not in the timing.
- Run on a quiet machine; SQL Server numbers move around a lot with concurrent load.

## Project layout

| File | Purpose |
| ---- | ------- |
| `Program.cs` | `BenchmarkSwitcher.FromAssembly(...).Run(args)` entry point |
| `BloggingContext.cs` | Duplicate of the test project's Blog/Post/Tag model (keeps benchmark independent of the test SDK dependency chain) |
| `BenchmarkFixture.cs` | `[GlobalSetup]` seeder — file-based SQLite, 1000/10/15 |
| `SplitQueryBenchmarks.cs` | Six benchmarks: 3 scenarios × {split=true, split=false} |

`IsPackable=false` and `IsTestProject=false` — this project is excluded from `dotnet test`
discovery and never produces a NuGet package.
