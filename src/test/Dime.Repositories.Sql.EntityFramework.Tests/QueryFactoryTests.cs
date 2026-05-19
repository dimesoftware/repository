using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public class QueryFactoryTests
    {
        [TestMethod]
        public void With_NullPredicate_ShouldReturnSourceUnchanged()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            IQueryable<Blog> result = ctx.Blogs.AsNoTracking().With((Expression<Func<Blog, bool>>)null);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public void WithFirst_NullPredicate_ShouldReturnFirstOrDefault()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            Blog result = ctx.Blogs.AsNoTracking().WithFirst(null);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void WithFirst_WithPredicate_ShouldReturnFirstMatch()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            Blog result = ctx.Blogs.AsNoTracking().WithFirst(b => b.Url.Contains("dogs"));

            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.BlogId);
        }

        [TestMethod]
        public void WithGroup_ExpressionKey_ShouldGroup()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);

            Expression<Func<Post, int>> keySelector = p => p.BlogId;
            IQueryable<IGrouping<int, Post>> grouped = ctx.Posts.AsNoTracking().WithGroup(keySelector);

            Assert.AreEqual(3, grouped.Count());
        }

        [TestMethod]
        public void WithGroup_FuncKey_ShouldGroup()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);
            List<Post> posts = ctx.Posts.AsNoTracking().ToList();

            Func<Post, int> keySelector = p => p.BlogId;
            IQueryable<IGrouping<int, Post>> grouped = posts.AsQueryable().WithGroup(keySelector);

            Assert.AreEqual(3, grouped.Count());
        }

        [TestMethod]
        public void WithGroup_OrderedEnumerable_FuncKey_ShouldGroup()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);
            List<Post> posts = ctx.Posts.AsNoTracking().ToList();

            IOrderedEnumerable<Post> ordered = posts.OrderBy(p => p.PostId);
            Func<Post, int> keySelector = p => p.BlogId;
            IQueryable<IGrouping<int, Post>> grouped = ordered.WithGroup(keySelector);

            Assert.AreEqual(3, grouped.Count());
        }

        [TestMethod]
        public void WithOrder_MultipleIOrders_ShouldChainSorts()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);

            IEnumerable<IOrder<Post>> orderBy = new List<IOrder<Post>>
            {
                new Order<Post>(nameof(Post.BlogId), true),
                new Order<Post>(nameof(Post.PostId), false)
            };

            IQueryable<Post> ordered = ctx.Posts.AsNoTracking().WithOrder(orderBy);

            List<Post> posts = ordered.ToList();
            Assert.AreEqual(5, posts.Count);
        }

        [TestMethod]
        public void WithOrder_MultipleIOrders_AscendingDescending_ShouldChainSorts()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);

            IEnumerable<IOrder<Post>> orderBy = new List<IOrder<Post>>
            {
                new Order<Post>(nameof(Post.BlogId), false),
                new Order<Post>(nameof(Post.PostId), true)
            };

            IQueryable<Post> ordered = ctx.Posts.AsNoTracking().WithOrder(orderBy);

            Assert.AreEqual(5, ordered.Count());
        }

        [TestMethod]
        public void WithOrder_MultipleExpressions_Ascending_ShouldChain()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);

            Expression<Func<Post, object>>[] orderBy =
            [
                p => p.BlogId,
                p => p.PostId
            ];

            IQueryable<Post> ordered = ctx.Posts.AsNoTracking().WithOrder(orderBy, ascending: true);

            Assert.AreEqual(5, ordered.Count());
        }

        [TestMethod]
        public void WithOrder_MultipleExpressions_Descending_ShouldChain()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);

            Expression<Func<Post, object>>[] orderBy =
            [
                p => p.BlogId,
                p => p.PostId
            ];

            IQueryable<Post> ordered = ctx.Posts.AsNoTracking().WithOrder(orderBy, ascending: false);

            Assert.AreEqual(5, ordered.Count());
        }

        [TestMethod]
        public void WithOrder_NullExpressionEnumerable_ShouldReturnSourceUnchanged()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            IQueryable<Blog> result = ctx.Blogs.AsNoTracking()
                .WithOrder((IEnumerable<Expression<Func<Blog, object>>>)null, ascending: true);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public void WithOrder_NullExpression_ShouldReturnSourceUnchanged()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            IQueryable<Blog> result = ctx.Blogs.AsNoTracking()
                .WithOrder((Expression<Func<Blog, object>>)null, ascending: true);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public void WithOrder_SingleExpression_Ascending_ShouldOrder()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            Expression<Func<Blog, object>> orderBy = b => b.Url;
            IQueryable<Blog> ordered = ctx.Blogs.AsNoTracking().WithOrder(orderBy, ascending: true);

            Assert.AreEqual("http://sample.com/catfish", ordered.First().Url);
        }

        [TestMethod]
        public void WithOrder_SingleExpression_Descending_ShouldOrder()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            Expression<Func<Blog, object>> orderBy = b => b.Url;
            IQueryable<Blog> ordered = ctx.Blogs.AsNoTracking().WithOrder(orderBy, ascending: false);

            Assert.AreEqual("http://sample.com/dogs", ordered.First().Url);
        }

        [TestMethod]
        public void WithOrder_FuncOrder_NullKey_ShouldUseDefault()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);
            List<Blog> blogs = ctx.Blogs.AsNoTracking().ToList();

            IQueryable<Blog> result = blogs.AsQueryable().WithOrder((Func<Blog, object>)null, ascending: true);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public void WithOrder_FuncOrder_NullKey_Descending_ShouldUseDefault()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);
            List<Blog> blogs = ctx.Blogs.AsNoTracking().ToList();

            IQueryable<Blog> result = blogs.AsQueryable().WithOrder((Func<Blog, object>)null, ascending: false);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public void WithOrder_FuncOrder_Ascending_ShouldOrder()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);
            List<Blog> blogs = ctx.Blogs.AsNoTracking().ToList();

            Func<Blog, object> orderBy = b => b.Url;
            IQueryable<Blog> result = blogs.AsQueryable().WithOrder(orderBy, ascending: true);

            Assert.AreEqual("http://sample.com/catfish", result.First().Url);
        }

        [TestMethod]
        public void WithOrder_FuncOrder_Descending_ShouldOrder()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);
            List<Blog> blogs = ctx.Blogs.AsNoTracking().ToList();

            Func<Blog, object> orderBy = b => b.Url;
            IQueryable<Blog> result = blogs.AsQueryable().WithOrder(orderBy, ascending: false);

            Assert.AreEqual("http://sample.com/dogs", result.First().Url);
        }

        [TestMethod]
        public void WithSelect_NullSelector_ShouldReturnDefault()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            IQueryable<BlogProjection> result = ctx.Blogs.AsNoTracking().WithSelect((Expression<Func<Blog, BlogProjection>>)null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void WithSelect_FuncSelector_ShouldProject()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);
            List<Blog> blogs = ctx.Blogs.AsNoTracking().ToList();

            Func<Blog, BlogProjection> selector = b => new BlogProjection { Url = b.Url };
            IQueryable<BlogProjection> result = blogs.AsQueryable().WithSelect(selector);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public void WithSelect_FuncSelector_Null_ShouldReturnDefault()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);
            List<Blog> blogs = ctx.Blogs.AsNoTracking().ToList();

            IQueryable<BlogProjection> result = blogs.AsQueryable().WithSelect((Func<Blog, BlogProjection>)null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void WithSelect_OrderedEnumerable_ShouldProject()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);
            IOrderedEnumerable<Blog> ordered = ctx.Blogs.AsNoTracking().ToList().OrderBy(b => b.BlogId);

            Func<Blog, BlogProjection> selector = b => new BlogProjection { Url = b.Url };
            IQueryable<BlogProjection> result = ordered.WithSelect(selector);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public void WithSelect_OrderedEnumerable_Null_ShouldReturnDefault()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);
            IOrderedEnumerable<Blog> ordered = ctx.Blogs.AsNoTracking().ToList().OrderBy(b => b.BlogId);

            IQueryable<BlogProjection> result = ordered.WithSelect((Func<Blog, BlogProjection>)null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void WithSelect_GroupingExpression_ShouldFlatten()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);
            List<Post> posts = ctx.Posts.AsNoTracking().ToList();

            Func<Post, int> keySelector = p => p.BlogId;
            IQueryable<IGrouping<int, Post>> grouped = posts.AsQueryable().WithGroup(keySelector);
            Expression<Func<IGrouping<int, Post>, IEnumerable<Post>>> select = g => g;

            IQueryable<Post> result = grouped.WithSelect<Post, Post, int>(select);

            Assert.AreEqual(5, result.Count());
        }

        [TestMethod]
        public void WithSelect_GroupingExpression_Null_ShouldReturnDefault()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);
            List<Post> posts = ctx.Posts.AsNoTracking().ToList();

            Func<Post, int> keySelector = p => p.BlogId;
            IQueryable<IGrouping<int, Post>> grouped = posts.AsQueryable().WithGroup(keySelector);

            IQueryable<Post> result = grouped.WithSelect<Post, Post, int>(null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void WithFirstSelect_NullSelector_ShouldReturnDefault()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);
            Blog blog = ctx.Blogs.AsNoTracking().First();

            BlogProjection result = blog.WithFirstSelect((Expression<Func<Blog, BlogProjection>>)null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void WithFirstSelect_ShouldProjectSingle()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);
            Blog blog = ctx.Blogs.AsNoTracking().First();

            BlogProjection result = blog.WithFirstSelect(b => new BlogProjection { Url = b.Url });

            Assert.IsNotNull(result);
            Assert.AreEqual(blog.Url, result.Url);
        }

        [TestMethod]
        public void Skip_With_PageSizeZero_ShouldReturnSourceUnchanged_DynamicOrderBy()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            Expression<Func<Blog, dynamic>> orderBy = b => b.BlogId;
            IQueryable<Blog> result = ctx.Blogs.AsNoTracking().With(page: 0, pageSize: 0, orderBy: orderBy);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public void Skip_With_NullOrderBy_DynamicVariant()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            IQueryable<Blog> result = ctx.Blogs.AsNoTracking().With(page: 1, pageSize: 2, orderBy: (Expression<Func<Blog, dynamic>>)null);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void Skip_With_IOrderListVariant()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            IEnumerable<IOrder<Blog>> orderBy = new List<IOrder<Blog>> { new Order<Blog>("BlogId", true) };
            IQueryable<Blog> result = ctx.Blogs.AsNoTracking().With(page: 1, pageSize: 2, orderBy: orderBy);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void Skip_With_ExpressionListVariant()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            IEnumerable<Expression<Func<Blog, object>>> orderBy = [b => b.BlogId];
            IQueryable<Blog> result = ctx.Blogs.AsNoTracking().With(page: 1, pageSize: 2, orderBy: orderBy);

            Assert.IsNotNull(result);
        }
    }
}