# ReactorData

[![Nuget](https://img.shields.io/nuget/v/ReactorData)](https://www.nuget.org/packages/ReactorData)

ReactorData is a fast, easy-to-use, low ceremony data container for your next .NET app.

Designed after SwiftData, it helps developers to persist data in UI applications abstracting the process of reading/writing to and from the storage.

1) Perfectly suited for declarative UI programming (MauiReactor, C# Markup, Comet etc) but can also be integrated in MVVM-base apps
2) Currently, it works with EFCore (Sqlite, SqlServer etc) or directly with Sqlite.
3) Easily expandable, you can create plugins for other storage libraries like LiteDB. You can even use a json file to store your models.
4) Non-intrusive, use your models

ReactorData can be used in any application .NET8+

## How it works:

Install ReactorData

```
<PackageReference Include="ReactorData" Version="X.X.X.X" />
```

Add it to your DI container

```csharp
services.AddReactorData();
```

Define your models using the `[Model]` attribute:

```csharp
[Model]
partial class Todo
{
    public int Id { get; set; }
    public string Title { get; set; }
    public bool Done { get; set; }
}
```

Get the model context from the container 

```csharp
var _modelContext = serviceProvider.GetService<IModelContext>();
```

Create a Query for the model:

```csharp
var _query = _modelContext.Query<Todo>();
```

A query is an object implementing INotifyCollectionChanged that notifies subscribers of any change to the list of models contained in the context.
You can freely create a query with custom linq, ordering results as you prefer:

```csharp
var _query = _modelContext.Query<Todo>(_=>_.Where(x => x.Done).OrderBy(x => x.Title));
```
Notifications are raised only for those models that pass the filter and in the order specified.

Now just add an entity to your context and the query will receive the notification.

```csharp
_modelContext.Add(new Todo { Title = "Task 1" });
_modelContext.Save();
```

Note that the `Save()` function is synchronous, it just signals to the context that you modified it. Entities are sent to the storage in a separate background thread.

## Sqlite storage

Without storage, ReactorData is more or less a state manager, keeping all the entities in memory.

Things are more interesting when configuring a storage plugin, such as Sqlite.

```
<PackageReference Include="ReactorData.Sqlite" Version="X.X.X.X" />
```
Configure the plugin by passing a connection string and the models you want it to manage:

```csharp
using ReactorData.Sqlite;
services.AddReactor(
    connectionString: $"Data Source=todo.db",
    configure: _ => _.Model<Todo>()
);
```

With these changes, entities are automatically saved as they are added/modified or deleted.

The last thing to do is to load Todo models from the storage when the app starts. 
You can decide when and which models to load, in this case, we want to load all the todo models at context startup.

```csharp
using ReactorData.Sqlite;
services.AddReactorData(
    connectionString: $"Data Source={_dbPath}",
    configure: _ => _.Model<Todo>(),
    modelContextConfigure: options =>
    {
        options.ConfigureContext = context => context.Load<Todo>();
    });
```

IModelContext.Load<T>() accepts a linq query that lets you specify which records to load.


## EFCore storage

The EFCore plugin allows you to use whatever supported data store you like (Sqlite, SQLServer, etc). Moreover, you can efficiently manage related entities.
Note that ReactorData works on top of EFCore without interfering with your existing code, which you may already use.

This is how to configure it:
```
<PackageReference Include="ReactorData.EFCore" Version="X.X.X.X" />
```

```csharp
using ReactorData.EFCore;
services.AddReactorData<TodoDbContext>(,
    modelContextConfigure: options =>
    {
        options.ConfigureContext = context => context.Load<Todo>();
    });
```

TodoDbContext is a normal DbContext like this:

```csharp
class TodoDbContext: DbContext
{
    public DbSet<Blog> Blogs => Set<Blog>();

    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options)
    { }
}
```

## MauiReactor

ReactorData perfectly integrates with MauiReactor (https://github.com/adospace/reactorui-maui)

This is a sample todo application featuring ReactorData:
https://github.com/adospace/mauireactor-samples/tree/main/TodoApp



https://github.com/adospace/reactor-data/assets/10573253/58dc1262-50f8-429e-ac69-6de46de5115f







