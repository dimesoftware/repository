using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
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
        public DbSet<ConcurrentItem> ConcurrentItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Blog>().HasKey(c => c.BlogId);
            modelBuilder.Entity<Tag>().HasKey(c => c.TagId);
            modelBuilder.Entity<ConcurrentItem>(b =>
            {
                b.HasKey(c => c.Id);
                b.Property(c => c.Token).IsConcurrencyToken();
            });
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
                optionsBuilder.UseSqlite("Data Source=blogging.db");
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

    public class ConcurrentItem
    {
        public int Id { get; set; }
        public string Value { get; set; }
        public string Token { get; set; }
    }
}