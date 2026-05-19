using System.Collections.Generic;
using System.Threading.Tasks;
using Bogus;

namespace Dime.Repositories.Sql.EntityFramework.IntegrationTests
{
    /// <summary>
    /// Generates a realistic-volume corpus of Blogs/Posts/Tags using Bogus.
    /// Output is seeded so each test run gets the same data — assertions over
    /// the bulk set are reproducible.
    /// </summary>
    internal static class BulkDataSeeder
    {
        internal const int BulkBlogCount = 1000;

        /// <summary>
        /// URLs produced here all start with this prefix so tests that target the
        /// deterministic seed can filter the bulk corpus out cleanly.
        /// </summary>
        internal const string BulkUrlPrefix = "https://bogus.example/";

        internal static async Task<BulkSeedSummary> SeedAsync(BloggingContext ctx)
        {
            // Deterministic seed for reproducible test runs.
            Randomizer.Seed = new System.Random(42);

            Faker<Blog> blogFaker = new Faker<Blog>()
                .RuleFor(b => b.Url, f => $"{BulkUrlPrefix}{f.Internet.DomainWord()}-{f.IndexGlobal}/blog/{f.Lorem.Slug()}")
                .RuleFor(b => b.Description, f => f.Random.Bool(0.7f) ? f.Lorem.Sentence() : null);

            List<Blog> blogs = blogFaker.Generate(BulkBlogCount);
            ctx.Blogs.AddRange(blogs);
            await ctx.SaveChangesAsync();

            // Posts per blog: random 0-10. Tags per blog: random 0-5.
            // Real-world planning-tasks shape has highly variable collection sizes;
            // this mirrors that variability.
            List<Post> posts = [];
            List<Tag> tags = [];
            Faker faker = new();

            foreach (Blog b in blogs)
            {
                int postCount = faker.Random.Int(0, 10);
                for (int i = 0; i < postCount; i++)
                    posts.Add(new Post
                    {
                        BlogId = b.BlogId,
                        Title = faker.Lorem.Sentence(wordCount: faker.Random.Int(3, 8)),
                        Content = faker.Lorem.Paragraph(),
                    });

                int tagCount = faker.Random.Int(0, 5);
                for (int i = 0; i < tagCount; i++)
                    tags.Add(new Tag
                    {
                        BlogId = b.BlogId,
                        Name = faker.Hacker.Noun(),
                    });
            }

            ctx.Posts.AddRange(posts);
            ctx.Tags.AddRange(tags);
            await ctx.SaveChangesAsync();

            return new BulkSeedSummary
            {
                BlogCount = blogs.Count,
                PostCount = posts.Count,
                TagCount = tags.Count,
            };
        }
    }

    public sealed class BulkSeedSummary
    {
        public int BlogCount { get; init; }
        public int PostCount { get; init; }
        public int TagCount { get; init; }
    }
}