using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public partial class PagedSyncTests
    {
        [TestMethod]
        public void FindAllPaged_OrderByList_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IEnumerable<IOrder<Blog>> orderBy = new List<IOrder<Blog>> { new Order<Blog>("BlogId", true) };
            Page<Blog> result = repo.FindAllPaged(
                where: b => true,
                orderBy: orderBy,
                page: 1,
                pageSize: 2);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }

        [TestMethod]
        public void FindAllPaged_OrderByListAndCountPredicate_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IEnumerable<IOrder<Blog>> orderBy = new List<IOrder<Blog>> { new Order<Blog>("BlogId", true) };
            Page<Blog> result = repo.FindAllPaged(
                where: b => b.Url.Contains("cat"),
                count: b => true,
                orderBy: orderBy,
                page: 1,
                pageSize: 10);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }

        [TestMethod]
        public void FindAllPaged_OrderByExpressionList_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IEnumerable<Expression<Func<Blog, object>>> orderBy = [b => b.BlogId];
            Page<Blog> result = repo.FindAllPaged(
                where: b => true,
                orderBy: orderBy,
                ascending: true,
                page: 1,
                pageSize: 2);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }

        [TestMethod]
        public void FindAllPaged_Projection_WithCountAndIOrderList_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IEnumerable<IOrder<Blog>> orderBy = new List<IOrder<Blog>> { new Order<Blog>("BlogId", true) };
            Page<BlogProjection> result = repo.FindAllPaged<BlogProjection>(
                where: b => b.Url.Contains("cat"),
                count: b => true,
                select: b => new BlogProjection { Url = b.Url },
                orderBy: orderBy,
                page: 1,
                pageSize: 10);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }

        [TestMethod]
        public void FindAllPaged_Projection_DynamicOrderBy_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            // Positional binding selects FindAllPaged<TResult>(where, select, Expression<TEntity,dynamic>, ascending, page, pageSize)
            Expression<Func<Blog, bool>> where = b => true;
            Expression<Func<Blog, BlogProjection>> select = b => new BlogProjection { Url = b.Url };
            Expression<Func<Blog, dynamic>> orderBy = b => b.BlogId;
            Page<BlogProjection> result = repo.FindAllPaged<BlogProjection>(where, select, orderBy, true, 1, 2);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }

        [TestMethod]
        public void FindAllPaged_Projection_IOrderListAndGroupBy_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            Expression<Func<Blog, bool>> where = b => true;
            Expression<Func<Blog, BlogProjection>> select = b => new BlogProjection { Url = b.Url };
            IEnumerable<IOrder<Blog>> orderBy = new List<IOrder<Blog>> { new Order<Blog>("BlogId", true) };
            Expression<Func<Blog, object>> groupBy = null;
            Page<BlogProjection> result = repo.FindAllPaged<BlogProjection>(where, select, orderBy, groupBy, true, 1, 2);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }

        [TestMethod]
        public void FindAllPaged_Entity_DynamicOrderBy_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            Expression<Func<Blog, bool>> where = b => true;
            Expression<Func<Blog, dynamic>> orderBy = b => b.BlogId;
            Page<Blog> result = repo.FindAllPaged(where, orderBy, true, 1, 2);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }

        [TestMethod]
        public void FindAllPaged_Entity_DynamicOrderByWithGroup_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            Expression<Func<Blog, bool>> where = b => true;
            Expression<Func<Blog, dynamic>> orderBy = b => b.BlogId;
            Expression<Func<Blog, object>> groupBy = null;
            Page<Blog> result = repo.FindAllPaged(where, orderBy, groupBy, true, 1, 2);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }
    }
}