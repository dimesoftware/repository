using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public class PageTests
    {
        [TestMethod]
        public void Default_Ctor_ShouldYieldEmptyState()
        {
            Page<int> page = new();

            Assert.IsNull(page.Data);
            Assert.AreEqual(0, page.Total);
            Assert.IsNull(page.Message);
            Assert.IsNull(page.Summary);
        }

        [TestMethod]
        public void DataCtor_ShouldSetDataAndEmptySummary()
        {
            Page<int> page = new([1, 2, 3]);

            Assert.AreEqual(3, page.Data.Count());
            Assert.IsNotNull(page.Summary);
            Assert.IsEmpty(page.Summary);
        }

        [TestMethod]
        public void DataTotalCtor_ShouldSetTotal()
        {
            Page<int> page = new([1, 2], 42);

            Assert.AreEqual(2, page.Data.Count());
            Assert.AreEqual(42, page.Total);
        }

        [TestMethod]
        public void DataTotalMessageCtor_ShouldSetMessage()
        {
            Page<int> page = new([1], 1, "hello");

            Assert.AreEqual("hello", page.Message);
        }

        [TestMethod]
        public void DataTotalMessageSummaryCtor_NullSummary_ShouldYieldEmpty()
        {
            Page<int> page = new([1], 1, "msg", summary: null);

            Assert.IsNotNull(page.Summary);
            Assert.IsEmpty(page.Summary);
        }

        [TestMethod]
        public void DataTotalMessageSummaryCtor_WithSummary_ShouldPopulate()
        {
            List<dynamic> summary = ["a", 1];
            Page<int> page = new([1], 1, "msg", summary);

            Assert.HasCount(2, page.Summary);
        }
    }
}