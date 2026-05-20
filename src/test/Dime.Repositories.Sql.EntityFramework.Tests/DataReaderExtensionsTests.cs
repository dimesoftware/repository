using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public class DataReaderExtensionsTests
    {
        [TestMethod]
        public void GetRecords_MapsColumnsToProperties()
        {
            using TestDatabase testDb = new();
            using SqliteCommand command = testDb.Connection.CreateCommand();
            command.CommandText = "SELECT BlogId, Url FROM Blogs ORDER BY BlogId";

            using SqliteDataReader reader = command.ExecuteReader();
            System.Collections.Generic.List<BlogRecord> records = reader.GetRecords<BlogRecord>();

            Assert.HasCount(3, records);
            Assert.AreEqual("http://sample.com/cats", records[0].Url);
        }

        [TestMethod]
        public void GetRecords_SkipsDbNullColumns()
        {
            using TestDatabase testDb = new();
            using SqliteCommand command = testDb.Connection.CreateCommand();
            command.CommandText = "SELECT BlogId, NULL AS Url FROM Blogs WHERE BlogId = 1";

            using SqliteDataReader reader = command.ExecuteReader();
            System.Collections.Generic.List<BlogRecord> records = reader.GetRecords<BlogRecord>();

            Assert.HasCount(1, records);
            Assert.IsNull(records[0].Url);
        }

        [TestMethod]
        public void GetRecords_IgnoresUnmatchedProperties()
        {
            using TestDatabase testDb = new();
            using SqliteCommand command = testDb.Connection.CreateCommand();
            command.CommandText = "SELECT BlogId FROM Blogs WHERE BlogId = 1";

            using SqliteDataReader reader = command.ExecuteReader();
            System.Collections.Generic.List<BlogRecord> records = reader.GetRecords<BlogRecord>();

            Assert.HasCount(1, records);
            Assert.AreEqual(1L, records[0].BlogId);
            Assert.IsNull(records[0].Url);
        }

        [TestMethod]
        public void HasColumn_True_WhenColumnExists()
        {
            using TestDatabase testDb = new();
            using SqliteCommand command = testDb.Connection.CreateCommand();
            command.CommandText = "SELECT BlogId, Url FROM Blogs";

            using SqliteDataReader reader = command.ExecuteReader();
            reader.Read();

            Assert.IsTrue(reader.HasColumn("BlogId"));
            Assert.IsTrue(reader.HasColumn("blogid"));
            Assert.IsTrue(reader.HasColumn("Url"));
        }

        [TestMethod]
        public void HasColumn_False_WhenColumnMissing()
        {
            using TestDatabase testDb = new();
            using SqliteCommand command = testDb.Connection.CreateCommand();
            command.CommandText = "SELECT BlogId FROM Blogs";

            using SqliteDataReader reader = command.ExecuteReader();
            reader.Read();

            Assert.IsFalse(reader.HasColumn("DoesNotExist"));
        }
    }

    public class BlogRecord
    {
        public long BlogId { get; set; }
        public string Url { get; set; }
    }
}