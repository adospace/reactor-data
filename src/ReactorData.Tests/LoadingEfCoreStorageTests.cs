using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using ReactorData.Tests.Models;
using ReactorData.EFCore;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using System.Collections.Specialized;

namespace ReactorData.Tests;

class LoadingEfCoreStorageTests
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
    public async Task TestContextLoadingUsingEfCoreStorage()
    {
        _container.Load<Blog>(query => query.Where(_ => _.Title.StartsWith("Stored")));

        await _container.Flush();

        //_container.Set<Blog>().Count.Should().Be(0);

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

        _container.FindByKey<Blog>(1).Should().NotBeNull();
    }


    [Test]
    public async Task TestContextWithRealatedEntitiesUsingEfCoreStorage()
    {
        _container.Load<Movie>();

        await _container.Flush();

        //_container.Set<Movie>().Count.Should().Be(0);

        var director = new Director { Name = "Martin Scorsese" };
        var movie = new Movie { Name = "The Irishman", Director = director };


        {
            using var scope = _services.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();

            dbContext.Add(movie);

            await dbContext.SaveChangesAsync();

            movie = dbContext.Movies.First();
        }

        var query = _container.Query<Movie>();

        bool addedEvent = false;
        void checkAddedEvent(object? sender, NotifyCollectionChangedEventArgs e)
        {
            e.Action.Should().Be(NotifyCollectionChangedAction.Add);
            e.NewItems.Should().NotBeNull();
            movie.IsEquivalentTo((Movie)e.NewItems![0]!).Should().BeTrue();
            e.NewStartingIndex.Should().Be(0);
            e.OldItems.Should().BeNull();

            addedEvent = true;
        };

        query.CollectionChanged += checkAddedEvent;

        _container.Load<Movie>(x => x.Include(_ => _.Director));

        await _container.Flush();

        addedEvent.Should().BeTrue();

        query.Count.Should().Be(1);

        query.CollectionChanged -= checkAddedEvent;

        var anotherMovie = new Movie { Name = "The Wolf of Wall Street", Director = director };

        bool addedAnotherMovieEvent = false;
        void checkAnotherMovieAddedEvent(object? sender, NotifyCollectionChangedEventArgs e)
        {
            e.Action.Should().Be(NotifyCollectionChangedAction.Add);
            e.NewItems.Should().NotBeNull();
            anotherMovie.IsEquivalentTo((Movie)e.NewItems![0]!).Should().BeTrue();
            e.NewStartingIndex.Should().Be(1);
            e.OldItems.Should().BeNull();

            addedAnotherMovieEvent = true;
        };

        query.CollectionChanged += checkAnotherMovieAddedEvent;

        _container.Add(anotherMovie);

        await _container.Flush();

        addedAnotherMovieEvent.Should().BeTrue();

        query.Count.Should().Be(2);

        query.CollectionChanged -= checkAnotherMovieAddedEvent;

        _container.Save();

        await _container.Flush();


    }
}
