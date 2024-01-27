using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReactorData.Tests.Models;
using ReactorData.EFCore;
using Microsoft.Data.Sqlite;
using System.Reflection.Metadata;

namespace ReactorData.Tests;

class BasicEfCoreStorageTests
{
    IServiceProvider _services;
    IModelContext _container;
    SqliteConnection _connection;

    [SetUp]
    public void Setup()
    {
        var serviceCollection = new ServiceCollection();
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        serviceCollection.AddReactorDataWithEfCore<TestDbContext>(options => options.UseSqlite(_connection));

        _services = serviceCollection.BuildServiceProvider();

        _container = _services.GetRequiredService<IModelContext>();
    }


    [TearDown]
    public void TearDown()
    {
        _connection.Dispose();
    }

    [Test]
    public async Task BasicOperationsOnEntityUsingEfCoreStorage()
    {
        var blog = new Blog { Title = "My new blog" };

        _container.GetEntityStatus(blog).Should().Be(EntityStatus.Detached);

        _container.Add(blog);

        await _container.Flush();

        _container.GetEntityStatus(blog).Should().Be(EntityStatus.Added);

        _container.Set<Blog>().Single().Should().BeSameAs(blog);

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

    [Test]
    public async Task BasicOperationsOnEntityWithRelationshipUsingEfCoreStorage()
    {
        var director = new Director { Name = "Martin Scorsese" };
        var movie = new Movie { Name = "The Irishman", Director = director };

        _container.GetEntityStatus(movie).Should().Be(EntityStatus.Detached);

        _container.Add(movie);

        await _container.Flush();

        _container.GetEntityStatus(movie).Should().Be(EntityStatus.Added);

        _container.Set<Movie>().Single().Should().BeSameAs(movie);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(movie).Should().Be(EntityStatus.Attached);

    }
}
