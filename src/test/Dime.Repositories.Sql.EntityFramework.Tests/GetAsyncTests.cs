using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public partial class GetAsyncTests
    {
        [TestMethod]
        public async Task FindAllAsync_Contains_ShouldFindMatches()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            IEnumerable<Blog> result = await repo.FindAllAsync(x => x.Url.Contains("cat"));
            Assert.AreEqual(2, result.Count());
        }
    }
}