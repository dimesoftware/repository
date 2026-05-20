using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Dime.Repositories.Benchmarks
{
    /// <summary>
    /// Benchmarks the splitQuery: true vs splitQuery: false branch of
    /// <c>EfRepository.FindAllPagedAsync</c> for the entity overload, against a fixture
    /// where each Blog has 10 Posts and 15 Tags (150 join-row Cartesian per Blog when joined).
    ///
    /// SQLite caveat: numbers here are NOT representative of SQL Server. The *relative* delta
    /// between split and single mode should still be visible (materialization + traversal cost
    /// scales with the Cartesian product regardless of driver), but treat absolute numbers as
    /// indicative only. See README.md.
    /// </summary>
    [MemoryDiagnoser]
    [MarkdownExporter]
    [SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 1, iterationCount: 3, invocationCount: 1)]
    public class SplitQueryBenchmarks
    {
        private BenchmarkFixture _fixture;

        private static readonly IEnumerable<IOrder<Blog>> OrderByUrl = [new Order<Blog>(nameof(Blog.Url), true)];

        private static readonly string[] BothIncludes = [nameof(Blog.Posts), nameof(Blog.Tags)];

        [GlobalSetup]
        public void Setup()
        {
            _fixture = new BenchmarkFixture();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _fixture?.Dispose();
            _fixture = null;
        }

        private EfRepository<Blog, BloggingContext> NewRepo()
            => new(_fixture.CreateContext());

        // ---------- Page 1, size 50 (production-shaped) ----------

        [Benchmark(Description = "Page1Of50 + Posts+Tags includes, splitQuery=false")]
        public async Task<int> Page1Of50_Single()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();
            IPage<Blog> page = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: OrderByUrl,
                page: 1,
                pageSize: 50,
                splitQuery: false,
                includes: BothIncludes);
            return page.Total;
        }

        [Benchmark(Description = "Page1Of50 + Posts+Tags includes, splitQuery=true")]
        public async Task<int> Page1Of50_Split()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();
            IPage<Blog> page = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: OrderByUrl,
                page: 1,
                pageSize: 50,
                splitQuery: true,
                includes: BothIncludes);
            return page.Total;
        }

        // ---------- Page 1, size 10 (small page, roundtrip-sensitive) ----------

        [Benchmark(Description = "Page1Of10 + Posts+Tags includes, splitQuery=false")]
        public async Task<int> Page1Of10_Single()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();
            IPage<Blog> page = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: OrderByUrl,
                page: 1,
                pageSize: 10,
                splitQuery: false,
                includes: BothIncludes);
            return page.Total;
        }

        [Benchmark(Description = "Page1Of10 + Posts+Tags includes, splitQuery=true")]
        public async Task<int> Page1Of10_Split()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();
            IPage<Blog> page = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: OrderByUrl,
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: BothIncludes);
            return page.Total;
        }

        // ---------- Full scan, all 1,000 Blogs (worst-case fan-out) ----------

        [Benchmark(Description = "FullScan(1000) + Posts+Tags includes, splitQuery=false")]
        public async Task<int> FullScan_Single()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();
            IPage<Blog> page = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: OrderByUrl,
                page: 1,
                pageSize: BenchmarkFixture.BlogCount,
                splitQuery: false,
                includes: BothIncludes);
            return page.Total;
        }

        [Benchmark(Description = "FullScan(1000) + Posts+Tags includes, splitQuery=true")]
        public async Task<int> FullScan_Split()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();
            IPage<Blog> page = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: OrderByUrl,
                page: 1,
                pageSize: BenchmarkFixture.BlogCount,
                splitQuery: true,
                includes: BothIncludes);
            return page.Total;
        }
    }
}