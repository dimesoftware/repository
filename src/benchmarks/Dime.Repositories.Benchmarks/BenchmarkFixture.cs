using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace Dime.Repositories.Benchmarks
{
    /// <summary>
    /// File-based SQLite fixture. Seeds 1,000 Blogs x ~10 Posts x ~15 Tags
    /// (10k Posts + 15k Tags; Cartesian fan-out of 150 join rows per Blog
    /// when both collections are joined in a single query).
    /// </summary>
    internal sealed class BenchmarkFixture : IDisposable
    {
        public const int BlogCount = 1000;
        public const int PostsPerBlog = 10;
        public const int TagsPerBlog = 15;

        public string DbPath { get; }
        public string ConnectionString { get; }
        public DbContextOptions<BloggingContext> Options { get; }

        public BenchmarkFixture()
        {
            DbPath = Path.Combine(
                Path.GetTempPath(),
                $"dime-repo-bench-{Guid.NewGuid():N}.db");
            ConnectionString = $"Data Source={DbPath}";

            Options = new DbContextOptionsBuilder<BloggingContext>()
                .UseSqlite(ConnectionString)
                .Options;

            Seed();
        }

        private void Seed()
        {
            using BloggingContext context = new(Options);
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            // Disable change tracking on the seed context — pure inserts.
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            List<Blog> blogs = new(BlogCount);
            for (int i = 0; i < BlogCount; i++)
                blogs.Add(new Blog { Url = $"http://sample.com/blog/{i}" });

            context.Blogs.AddRange(blogs);
            context.SaveChanges();

            // Seed Posts and Tags in batches to keep memory bounded.
            const int batchSize = 200;
            for (int start = 0; start < blogs.Count; start += batchSize)
            {
                int end = Math.Min(start + batchSize, blogs.Count);

                for (int i = start; i < end; i++)
                {
                    int blogId = blogs[i].BlogId;

                    for (int p = 0; p < PostsPerBlog; p++)
                    {
                        context.Posts.Add(new Post
                        {
                            BlogId = blogId,
                            Title = $"Post {p} for blog {blogId}",
                            Content = $"Content body for post {p} of blog {blogId}. Padded so allocations are noticeable."
                        });
                    }

                    for (int t = 0; t < TagsPerBlog; t++)
                    {
                        context.Tags.Add(new Tag
                        {
                            BlogId = blogId,
                            Name = $"tag-{t}-blog-{blogId}"
                        });
                    }
                }

                context.SaveChanges();
                context.ChangeTracker.Clear();
            }
        }

        public BloggingContext CreateContext() => new(Options);

        public void Dispose()
        {
            try
            {
                // Force SQLite to release the file handle on Windows.
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (File.Exists(DbPath))
                    File.Delete(DbPath);
            }
            catch
            {
                // Best-effort cleanup; benchmark teardown shouldn't fail the run.
            }
        }
    }
}