using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using ReactorData.Tests.Models;
using ReactorData.EFCore;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using System.Collections.Specialized;

namespace ReactorData.Tests;

class LoadingTests
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
        serviceCollection.AddReactorData<TestDbContext>(options => options.UseSqlite(_connection));

        _services = serviceCollection.BuildServiceProvider();

        _container = _services.GetRequiredService<IModelContext>();
    }


    [TearDown]
    public void TearDown()
    {

    }

    [Test]
    public async Task TestContextLoading()
    {
        _container.Load<Blog>(query => query.Where(_ => _.Title.StartsWith("Stored")));

        await _container.Flush();

        _container.Set<Blog>().Count().Should().Be(0);

        var firstBlog = new Blog { Title = "Stored Blog" };

        {
            using var scope = _services.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            dbContext.Add(firstBlog);

            await dbContext.SaveChangesAsync();

            firstBlog = dbContext.Blogs.First();
        }

        var query = _container.Query<Blog>();

        bool addedEvent = false;
        void checkAddedEvent(object? sender, NotifyCollectionChangedEventArgs e)
        {
            e.Action.Should().Be(NotifyCollectionChangedAction.Add);
            e.NewItems.Should().NotBeNull();
            e.NewItems![0].Should().BeEquivalentTo(firstBlog);
            e.NewStartingIndex.Should().Be(0);
            e.OldItems.Should().BeNull();

            addedEvent = true;
        };

        query.CollectionChanged += checkAddedEvent;

        _container.Load<Blog>(query => query.Where(_ => _.Title.StartsWith("Stored")));
        
        await _container.Flush();

        addedEvent.Should().BeTrue();

        query.Count.Should().Be(1);

        query.CollectionChanged -= checkAddedEvent;

        var modifiedBlog = new Blog { Id = firstBlog.Id, Title = "Stored Blog edited in db context" };

        {
            using var scope = _services.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            dbContext.Update(modifiedBlog);

            await dbContext.SaveChangesAsync();
        }

        {
            bool notCalledEvent = true;
            void checkNotCalledEvent(object? sender, NotifyCollectionChangedEventArgs e)
            {
                notCalledEvent = false;
            };

            query.CollectionChanged += checkNotCalledEvent;

            _container.Load<Blog>(query => query.Where(_ => _.Title.StartsWith("Stored")));

            await _container.Flush();

            notCalledEvent.Should().BeTrue();

            query.Count.Should().Be(1);

            query.CollectionChanged -= checkNotCalledEvent;
        }

        {
            bool udpatedEvent = false;
            void checkUpdatedEvent(object? sender, NotifyCollectionChangedEventArgs e)
            {
                e.Action.Should().Be(NotifyCollectionChangedAction.Replace);
                e.NewItems.Should().NotBeNull();
                e.NewItems![0].Should().BeEquivalentTo(modifiedBlog);
                e.NewStartingIndex.Should().Be(0);
                e.OldItems.Should().NotBeNull();
                e.OldItems![0].Should().BeEquivalentTo(firstBlog);
                e.OldStartingIndex.Should().Be(0);

                udpatedEvent = true;
            };

            query.CollectionChanged += checkUpdatedEvent;

            _container.Load<Blog>(query => query.Where(_ => _.Title.StartsWith("Stored")), (x1, x2) => x1.Title == x2.Title);

            await _container.Flush();

            udpatedEvent.Should().BeTrue();

            query.Count.Should().Be(1);

            query.CollectionChanged -= checkUpdatedEvent;
        }
    }
}
