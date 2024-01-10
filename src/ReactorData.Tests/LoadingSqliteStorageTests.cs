using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using ReactorData.Tests.Models;
using FluentAssertions;
using System.Collections.Specialized;
using ReactorData.Sqlite;
using System.Collections.Generic;

namespace ReactorData.Tests;

class LoadingSqliteStorageTests
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
    public async Task TestContextLoadingUsingSqliteStorage()
    {
        _container.Load<Blog>(query => query.Where(_ => _.Title.StartsWith("Stored")));

        await _container.Flush();

        _container.Set<Blog>().Count().Should().Be(0);

        var firstBlog = new Blog { Title = "Stored Blog" };

        {
            using var command = _connection.CreateCommand();

            command.CommandText = $$"""
                                INSERT INTO Blog (MODEL) VALUES ($json) RETURNING ROWID
                                """;
            command.Parameters.AddWithValue("$json", "{}");

            var key = await command.ExecuteScalarAsync();

            firstBlog.Id = (int)Convert.ChangeType(key!, typeof(int));

            var json = System.Text.Json.JsonSerializer.Serialize(firstBlog);
            command.CommandText = $$"""
                                UPDATE Blog SET MODEL = $json WHERE ID = $id
                                """;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$id", firstBlog.Id);
            command.Parameters.AddWithValue("$json", json);

            await command.ExecuteNonQueryAsync();
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
            using var command = _connection.CreateCommand();

            var json = System.Text.Json.JsonSerializer.Serialize(modifiedBlog);
            command.CommandText = $$"""
                                UPDATE Blog SET MODEL = $json WHERE ID = $id
                                """;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$id", modifiedBlog.Id);
            command.Parameters.AddWithValue("$json", json);

            await command.ExecuteNonQueryAsync();
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
}
