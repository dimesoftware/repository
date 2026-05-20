using Microsoft.VisualStudio.TestTools.UnitTesting;

// Unit tests run against per-test SQLite in-memory connections, so they are independent and
// safe to parallelise at the method level. MSTEST0001 requires this to be explicit.
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
