using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    internal class TestDatabase : IDisposable
    {
        internal TestDatabase(bool seedPosts = false, bool seedTags = false, bool captureSql = false)
        {
            // In-memory database only exists while the connection is open
            Connection = new("DataSource=:memory:");
            Connection.Open();

            CapturedSql = new List<string>();

            DbContextOptionsBuilder<BloggingContext> builder = new DbContextOptionsBuilder<BloggingContext>()
                .UseSqlite(Connection);

            if (captureSql)
            {
                builder
                    .EnableSensitiveDataLogging()
                    .LogTo(
                        s => CapturedSql.Add(s),
                        [DbLoggerCategory.Database.Command.Name],
                        LogLevel.Information);
            }

            Options = builder.Options;

            CreateDatabase(seedPosts, seedTags);
        }

        internal SqliteConnection Connection { get; private set; }

        internal DbContextOptions<BloggingContext> Options { get; private set; }

        internal List<string> CapturedSql { get; private set; }

        internal void CreateDatabase(bool seedPosts, bool seedTags)
        {
            using (BloggingContext context = new(Options))
                context.Database.EnsureCreated();

            using (BloggingContext context = new(Options))
            {
                Blog cats = new() { Url = "http://sample.com/cats" };
                Blog catfish = new() { Url = "http://sample.com/catfish" };
                Blog dogs = new() { Url = "http://sample.com/dogs" };
                context.Blogs.Add(cats);
                context.Blogs.Add(catfish);
                context.Blogs.Add(dogs);
                context.SaveChanges();

                if (seedPosts)
                {
                    context.Posts.Add(new Post { BlogId = cats.BlogId, Title = "Cat post 1", Content = "Meow" });
                    context.Posts.Add(new Post { BlogId = cats.BlogId, Title = "Cat post 2", Content = "Purr" });
                    context.Posts.Add(new Post { BlogId = catfish.BlogId, Title = "Catfish post", Content = "Swim" });
                    context.Posts.Add(new Post { BlogId = dogs.BlogId, Title = "Dog post 1", Content = "Woof" });
                    context.Posts.Add(new Post { BlogId = dogs.BlogId, Title = "Dog post 2", Content = "Bark" });
                    context.SaveChanges();
                }

                if (seedTags)
                {
                    context.Tags.Add(new Tag { BlogId = cats.BlogId, Name = "feline" });
                    context.Tags.Add(new Tag { BlogId = cats.BlogId, Name = "cute" });
                    context.Tags.Add(new Tag { BlogId = cats.BlogId, Name = "indoor" });
                    context.Tags.Add(new Tag { BlogId = catfish.BlogId, Name = "aquatic" });
                    context.Tags.Add(new Tag { BlogId = dogs.BlogId, Name = "canine" });
                    context.Tags.Add(new Tag { BlogId = dogs.BlogId, Name = "loyal" });
                    context.SaveChanges();
                }
            }
        }

        public void Dispose()
        {
            Connection.Dispose();
        }
    }
}