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