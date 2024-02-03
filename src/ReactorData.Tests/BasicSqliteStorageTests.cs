using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ReactorData.Tests.Models;
using ReactorData.Sqlite;
using Microsoft.Data.Sqlite;

namespace ReactorData.Tests;

class BasicSqliteStorageTests
{
    IServiceProvider _services;
    IModelContext _container;
    private SqliteConnection _connection;

    [SetUp]
    public void Setup()
    {
        var serviceCollection = new ServiceCollection();
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        serviceCollection.AddReactorDataWithSqlite(_connection, 
            configuration => configuration.Model<Blog>());

        _services = serviceCollection.BuildServiceProvider();

        _container = _services.GetRequiredService<IModelContext>();
    }


    [TearDown]
    public void TearDown()
    {
        _connection.Dispose();
    }

    [Test]
    public async Task BasicOperationsOnEntityUsingSqliteStorage()
    {
        var blog = new Blog { Title = "My new blog" };

        _container.GetEntityStatus(blog).Should().Be(EntityStatus.Detached);

        _container.Add(blog);

        await _container.Flush();

        _container.GetEntityStatus(blog).Should().Be(EntityStatus.Added);

        //_container.Set<Blog>().Single().Should().BeSameAs(blog);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(blog).Should().Be(EntityStatus.Attached);

        blog.Title = "My new blog modified";

        _container.Update(blog);

        await _container.Flush();

        _container.GetEntityStatus(blog).Should().Be(EntityStatus.Updated);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(blog).Should().Be(EntityStatus.Attached);

        _container.Delete(blog);

        await _container.Flush();

        _container.GetEntityStatus(blog).Should().Be(EntityStatus.Deleted);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(blog).Should().Be(EntityStatus.Detached);
    }
}
