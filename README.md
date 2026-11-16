<p align="center"><img src="assets/logo.png" width="350" alt="Logo"></p>

<div align="center">
<h1>Repositories</h1>
</div>

Implementation of the repository pattern with Entity Framework.


## About this project

Generic repository pattern with an implementation using Entity Framework. This project revolves around the `IRepository<T>` interface. This interfaces defines the capabilities of a repository which - rather unsurprisingly - are simple CRUD operations.

In addition, this project is also concerned with instantiating the repositories. Rather than accessing the repository's implementation directly, a repository factory (defined by `IRepositoryFactory`) can be used and injected into the application.

The projects in the `Providers` folder provide the implementation of the contracts defined in the Dime.Repositories assembly.

## Getting started

Use the package manager NuGet to install Dime.Repositories:

`dotnet add package Dime.Repositories`

## Upgrading to 3.5

3.5 changes the exceptions that writes raise on SQL Server. In 3.4 a failed save surfaced as a raw `DbUpdateException`, and a failed set-based `UpdateAsync(where, ...)` or `DeleteAsync(where)` as a raw `SqlException`. From 3.5 every repository write maps them:

- a foreign key, unique index or unique constraint violation (SQL Server errors 547, 2601 and 2627, including primary keys) raises a `ConstraintViolationException`;
- any other SQL Server error raises a `DatabaseAccessException`;
- the original exception stays available as `InnerException`, so a `DbUpdateException` and its `Entries` are still reachable.

Replace `catch (DbUpdateException)` and filters on `SqlException.Number` around repository writes with these types. Optimistic concurrency conflicts are unchanged: the repository resolves them with its `SaveStrategy`.

`ITransactionalRepositoryFactory` gains `ExecuteInTransactionAsync`, so a class that implements the interface itself must add it; see [Transactions](#transactions).

## Usage

Here's a simple example which demonstrates the usage of the repository.

``` csharp
using Dime.Repositories;

public class CustomerService
{
  private readonly IRepository<Customer> _repository;
  public CustomerService(IRepository<Customer> repository)
  {
    _repository = repository;
  }

  public async IEnumerable<Customer> GetCustomers()
    => await _repository_.FindAllAsync(x => x.IsActive == true);
}
```

### Targeted updates and grouped reads

Since 3.3.0 several properties can be updated in one `UPDATE` statement, without loading the entities:

``` csharp
int updated = await _repository.UpdateAsync(
    x => x.JobId == jobId,
    new PropertyUpdate<Job>()
        .Set(x => x.Status, JobStatus.Closed)
        .Set(x => x.IsOpen, false)
        .Set(x => x.Note, x => x.Note + " (closed)"));
```

Grouped reads run entirely in the database (one `GROUP BY` query, no in-memory grouping):

``` csharp
var counts = await _repository.FindGroupedAsync(
    x => x.IsOpen,
    x => new { x.Job.SourceApp, x.Job.SourceType, x.Status },
    g => new { g.Key.SourceApp, g.Key.SourceType, g.Key.Status, Count = g.Count() });
```

### Transactions

`EfRepositoryFactory<TContext>` implements `ITransactionalRepositoryFactory`. Repositories created by the transaction share one connection, so the work commits or rolls back as a whole without escalating to a distributed transaction. `AcquireLockAsync` (SQL Server only) takes an exclusive `sp_getapplock` lock that is held until the transaction ends:

``` csharp
await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
await transaction.AcquireLockAsync($"job:{jobId}");

IRepository<Job> jobs = transaction.Create<Job>();
await jobs.UpdateAsync(x => x.Id == jobId, new PropertyUpdate<Job>().Set(x => x.Status, JobStatus.Closed));

await transaction.CommitAsync();
```

Disposing the transaction without a commit rolls it back, as does `RollbackAsync`. A context configured with a retrying execution strategy (such as `EnableRetryOnFailure`) cannot start a transaction this way, because EF cannot replay it.

On such a context, hand the unit to `ExecuteInTransactionAsync` instead. It runs inside the execution strategy: each attempt gets a new connection and transaction, commits when the work completes and rolls back when it throws. On a transient failure the strategy replays the whole unit, so the work must have no side effects outside the database, and should be idempotent in case the connection drops during the commit:

``` csharp
await factory.ExecuteInTransactionAsync(async (transaction, cancellationToken) =>
{
    await transaction.AcquireLockAsync($"job:{jobId}", cancellationToken: cancellationToken);

    await transaction.Create<Job>().UpdateAsync(x => x.Id == jobId, new PropertyUpdate<Job>().Set(x => x.Status, JobStatus.Closed));
    await transaction.Create<JobLog>().CreateAsync(new JobLog { JobId = jobId, Message = "Closed" });
}, cancellationToken);
```

The unit commits and rolls back on its own: calling `CommitAsync` or `RollbackAsync` inside the work throws, so throw from the work to roll back, and disposing the transaction inside the work does nothing. Create every repository from the transaction the work is given: a repository from the factory saves on its own and would be applied again on a replay. Build entities and materialize results inside the work, observe the cancellation token it is given, and do not keep the transaction or its repositories after it returns. A transaction and its repositories are not thread-safe, so do not run their operations concurrently. The work must not start another unit or transaction either, since that one commits on its own and a replay would apply it twice. On a retrying context, a lock that is not granted fails the unit without a replay.

A generic overload returns the result of the attempt that committed.

`EfRepositoryFactory<TContext>` also takes a `verifySucceeded` delegate. After a transient failure the execution strategy calls it before replaying the unit, and skips the replay when it returns true, which covers a connection that drops while the commit is landing. It must return true only when this unit's writes are in the database, typically by looking up a row with a key the caller generated:

``` csharp
EfRepositoryFactory<AppDbContext> factory = new(contextFactory);
Guid logId = Guid.NewGuid();

await factory.ExecuteInTransactionAsync(
    (transaction, cancellationToken) => transaction.Create<JobLog>().CreateAsync(new JobLog { Id = logId, JobId = jobId, Message = "Closed" }),
    async cancellationToken =>
    {
        using IRepository<JobLog> logs = factory.Create<JobLog>();
        long count = await logs.CountAsync(x => x.Id == logId);
        return count == 1;
    },
    cancellationToken);
```

Inside a unit on a retrying context, write errors other than constraint violations are not wrapped in a `DatabaseAccessException`: they surface as EF raised them (a `DbUpdateException` or `SqlException`, or a `RetryLimitExceededException` once the retries run out), so the execution strategy can decide on a replay. Constraint violations raise a `ConstraintViolationException` and are never replayed.

## Build and Test

To run the solution, you will need:

- You must have Visual Studio 2022 Community or higher.
- The dotnet cli is also highly recommended.

To run the tests, you can use the trustee dotnet cli commands:

- Run dotnet restore
- Run dotnet build
- Run dotnet test
  
## Contributing

![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)

Pull requests are welcome. Please check out the contribution and code of conduct guidelines.

## License

[![License](http://img.shields.io/:license-mit-blue.svg?style=flat-square)](http://badges.mit-license.org)