using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Dime.Repositories.Benchmarks
{
    /// <summary>
    /// Copy of the Blog/Post/Tag model used in the unit tests. Duplicated here so the benchmark
    /// project doesn't take a dependency on the test project (which carries MSTest/Test SDK and
    /// would otherwise pollute the benchmark's runtime/build). Keep the schema in sync with
    /// <c>src/test/Dime.Repositories.Sql.EntityFramework.Tests/Helpers/BloggingContext.cs</c>.
    /// </summary>
    public class BloggingContext : DbContext
    {
        public BloggingContext()
        { }

        public BloggingContext(DbContextOptions<BloggingContext> options)
            : base(options)
        { }

        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Tag> Tags { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blog>().HasKey(c => c.BlogId);
            modelBuilder.Entity<Tag>().HasKey(c => c.TagId);
        }
    }

    public class Blog
    {
        public int BlogId { get; set; }
        public string Url { get; set; }

        public List<Post> Posts { get; set; }
        public List<Tag> Tags { get; set; }
    }

    public class Post
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public int BlogId { get; set; }
        public Blog Blog { get; set; }
    }

    public class Tag
    {
        public int TagId { get; set; }
        public string Name { get; set; }

        public int BlogId { get; set; }
        public Blog Blog { get; set; }
    }
}
