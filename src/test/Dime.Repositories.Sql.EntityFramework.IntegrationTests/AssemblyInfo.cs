using Microsoft.VisualStudio.TestTools.UnitTesting;

// Integration tests share a single Testcontainers SQL Server instance and a single seeded
// database. Tests assert against global totals (SqlServerFixture.TotalBlogCount) and
// captured SQL command logs — both of which race under parallel execution.
[assembly: DoNotParallelize]
