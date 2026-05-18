using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MsSql;

namespace Dime.Repositories.Sql.EntityFramework.IntegrationTests
{
    /// <summary>
    /// Assembly-level fixture that starts a single SQL Server container shared across all integration tests.
    /// Boot cost (~20-30s) is amortized; individual tests share the same database.
    /// </summary>
    [TestClass]
    public static class SqlServerFixture
    {
        public static MsSqlContainer Container { get; private set; }

        public static string ConnectionString { get; private set; }

        /// <summary>The number of deterministic Blogs seeded (with URLs like "http://sample.com/cats", "Cats", etc.).</summary>
        public const int DeterministicBlogCount = 7;

        /// <summary>Bulk-corpus seed summary; populated after Bogus seeds run.</summary>
        public static BulkSeedSummary BulkSummary { get; private set; }

        public static int TotalBlogCount => DeterministicBlogCount + (BulkSummary?.BlogCount ?? 0);

        [AssemblyInitialize]
        public static async Task AssemblyInit(TestContext _)
        {
            Container = new MsSqlBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                .Build();

            await Container.StartAsync();
            ConnectionString = Container.GetConnectionString();

            // Build schema and seed once for the whole assembly.
            await using BloggingContext ctx = CreateContext();
            await ctx.Database.EnsureCreatedAsync();

            // Define inline table-valued functions used by the TVF tests.
            await ctx.Database.ExecuteSqlRawAsync(@"
CREATE FUNCTION dbo.fGetBlogsWithUrlContaining(@needle NVARCHAR(50))
RETURNS TABLE
AS
RETURN
(
    SELECT [BlogId], [Url], [Description]
    FROM [Blogs]
    WHERE [Url] LIKE '%' + @needle + '%'
);");

            await ctx.Database.ExecuteSqlRawAsync(@"
CREATE FUNCTION dbo.fGetBlogsByJsonUrls(@urls NVARCHAR(MAX))
RETURNS TABLE
AS
RETURN
(
    SELECT b.[BlogId], b.[Url], b.[Description]
    FROM [Blogs] b
    INNER JOIN OPENJSON(@urls) WITH ([url] NVARCHAR(200) '$') j ON b.[Url] = j.[url]
);");

            await ctx.Database.ExecuteSqlRawAsync(@"
CREATE FUNCTION dbo.fGetAllBlogs()
RETURNS TABLE
AS
RETURN
(
    SELECT [BlogId], [Url], [Description]
    FROM [Blogs]
);");

            // Seed deterministic data.
            Blog cats = new() { Url = "http://sample.com/cats", Description = "Feline blog" };
            Blog catfish = new() { Url = "http://sample.com/catfish", Description = null };
            Blog dogs = new() { Url = "http://sample.com/dogs", Description = "Canine blog" };
            Blog mixedCase1 = new() { Url = "Cats", Description = "Mixed case A" };
            Blog mixedCase2 = new() { Url = "cats", Description = null };
            Blog mixedCase3 = new() { Url = "CATS", Description = "Mixed case C" };
            Blog mixedCase4 = new() { Url = "Dogs", Description = "Mixed case D" };

            ctx.Blogs.AddRange(cats, catfish, dogs, mixedCase1, mixedCase2, mixedCase3, mixedCase4);
            await ctx.SaveChangesAsync();

            // Seed posts/tags for the cats blog (used by split-query tests).
            ctx.Posts.AddRange(
                new Post { BlogId = cats.BlogId, Title = "Cat post 1", Content = "Meow" },
                new Post { BlogId = cats.BlogId, Title = "Cat post 2", Content = "Purr" },
                new Post { BlogId = cats.BlogId, Title = "Cat post 3", Content = "Hiss" });
            ctx.Tags.AddRange(
                new Tag { BlogId = cats.BlogId, Name = "feline" },
                new Tag { BlogId = cats.BlogId, Name = "cute" },
                new Tag { BlogId = cats.BlogId, Name = "indoor" },
                new Tag { BlogId = cats.BlogId, Name = "fluffy" });
            await ctx.SaveChangesAsync();

            // Layer a realistic-volume Bogus-generated corpus on top.
            // URLs are prefixed with BulkDataSeeder.BulkUrlPrefix so tests over the deterministic
            // seed can cleanly exclude this bulk data.
            BulkSummary = await BulkDataSeeder.SeedAsync(ctx);
        }

        [AssemblyCleanup]
        public static async Task AssemblyCleanupAsync()
        {
            if (Container != null)
            {
                await Container.DisposeAsync();
                Container = null;
            }
        }

        public static BloggingContext CreateContext(List<string> capturedSql = null)
        {
            DbContextOptionsBuilder<BloggingContext> builder = new DbContextOptionsBuilder<BloggingContext>()
                .UseSqlServer(ConnectionString);

            if (capturedSql != null)
            {
                builder
                    .EnableSensitiveDataLogging()
                    .LogTo(
                        s => capturedSql.Add(s),
                        [DbLoggerCategory.Database.Command.Name],
                        LogLevel.Information);
            }

            return new BloggingContext(builder.Options);
        }
    }
}