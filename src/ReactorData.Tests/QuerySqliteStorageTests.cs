﻿using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using ReactorData.Tests.Models;
using ReactorData.EFCore;
using System.Collections.Specialized;
using Microsoft.EntityFrameworkCore;
using ReactorData.Sqlite;

namespace ReactorData.Tests;

class QuerySqliteStorageTests
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

        serviceCollection.AddReactorData(_connection,
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
    public async Task TestQueryFunctionsUsingSqliteStorage()
    {
        var firstBlog = new Blog { Title = "My new blog" };

        _container.GetEntityStatus(firstBlog).Should().Be(EntityStatus.Detached);

        _container.Add(firstBlog);

        _container.Save();

        await _container.Flush();

        var queryFirst = _container.Query<Blog>(query => query.Where(_ => _.Title.StartsWith("My")).OrderBy(_=>_.Title));
        var querySecond = _container.Query<Blog>(query => query.Where(_ => _.Title.Contains("second")).OrderBy(_ => _.Title));

        queryFirst.Count.Should().Be(1);
        querySecond.Count.Should().Be(0);

        {
            bool removedEvent = false;
            void checkRemovedEvent(object? sender, NotifyCollectionChangedEventArgs e)
            {
                e.Action.Should().Be(NotifyCollectionChangedAction.Remove);
                e.OldItems.Should().NotBeNull();
                e.OldItems![0].Should().BeSameAs(firstBlog);
                e.OldStartingIndex.Should().Be(0);
                e.NewItems.Should().BeNull();

                removedEvent = true;
            };

            queryFirst.CollectionChanged += checkRemovedEvent;

            bool notCalledEvent = true;
            void checkNotCalledEvent(object? sender, NotifyCollectionChangedEventArgs e)
            {
                notCalledEvent = false;
            };

            querySecond.CollectionChanged += checkNotCalledEvent;

            _container.Replace(firstBlog, new Blog { Id = firstBlog.Id, Title = "(edited)" + firstBlog.Title });

            _container.Save();

            await _container.Flush();

            queryFirst.Count.Should().Be(0);

            removedEvent.Should().BeTrue();
            notCalledEvent.Should().BeTrue();

            queryFirst.CollectionChanged -= checkRemovedEvent;
            querySecond.CollectionChanged -= checkNotCalledEvent;
        }

        {
            var secondBlog = new Blog { Title = "My second blog" };

            bool addedFirstQueryEvent = false;
            void checkAddedFirstQueryEvent(object? sender, NotifyCollectionChangedEventArgs e)
            {
                e.Action.Should().Be(NotifyCollectionChangedAction.Add);
                e.NewItems.Should().NotBeNull();
                e.NewItems![0].Should().BeSameAs(secondBlog);
                e.NewStartingIndex.Should().Be(0);
                e.OldItems.Should().BeNull();

                addedFirstQueryEvent = true;
            };

            queryFirst.CollectionChanged += checkAddedFirstQueryEvent;

            bool addedSecondQueryEvent = false;
            void checkAddedSecondQueryEvent(object? sender, NotifyCollectionChangedEventArgs e)
            {
                e.Action.Should().Be(NotifyCollectionChangedAction.Add);
                e.NewItems.Should().NotBeNull();
                e.NewItems![0].Should().BeSameAs(secondBlog);
                e.NewStartingIndex.Should().Be(0);
                e.OldItems.Should().BeNull();

                addedSecondQueryEvent = true;
            };

            querySecond.CollectionChanged += checkAddedSecondQueryEvent;

            _container.Add(secondBlog);

            _container.Save();

            await _container.Flush();

            queryFirst.Count.Should().Be(1);
            querySecond.Count.Should().Be(1);

            addedFirstQueryEvent.Should().BeTrue();
            addedSecondQueryEvent.Should().BeTrue();

            queryFirst.CollectionChanged -= checkAddedFirstQueryEvent;
            querySecond.CollectionChanged -= checkAddedSecondQueryEvent;
        }

        {
            firstBlog.Title = "My new blog";

            bool addedFirstQueryEvent = false;
            void checkAddedFirstQueryEvent(object? sender, NotifyCollectionChangedEventArgs e)
            {
                e.Action.Should().Be(NotifyCollectionChangedAction.Add);
                e.NewItems.Should().NotBeNull();
                e.NewItems![0].Should().BeSameAs(firstBlog);
                e.NewStartingIndex.Should().Be(0);
                e.OldItems.Should().BeNull();

                addedFirstQueryEvent = true;
            };

            queryFirst.CollectionChanged += checkAddedFirstQueryEvent;

            bool notCalledSecondQueryEvent = true;
            void checkAddedSecondQueryEvent(object? sender, NotifyCollectionChangedEventArgs e)
            {
                notCalledSecondQueryEvent = false;
            };

            querySecond.CollectionChanged += checkAddedSecondQueryEvent;

            _container.Replace(firstBlog, firstBlog);

            _container.Save();

            await _container.Flush();

            queryFirst.Count.Should().Be(2);
            querySecond.Count.Should().Be(1);

            addedFirstQueryEvent.Should().BeTrue();
            notCalledSecondQueryEvent.Should().BeTrue();

            queryFirst.CollectionChanged -= checkAddedFirstQueryEvent;
            querySecond.CollectionChanged -= checkAddedSecondQueryEvent;
        }

        {
            firstBlog.Title += " (updated)";

            bool updatedFirstQueryEvent = false;
            void checkUpdatedFirstQueryEvent(object? sender, NotifyCollectionChangedEventArgs e)
            {
                e.Action.Should().Be(NotifyCollectionChangedAction.Replace);
                e.NewItems.Should().NotBeNull();
                e.NewItems![0].Should().BeSameAs(firstBlog);
                e.NewStartingIndex.Should().Be(0);
                e.OldItems.Should().NotBeNull();
                e.OldItems![0].Should().BeSameAs(firstBlog);
                e.OldStartingIndex.Should().Be(0);

                updatedFirstQueryEvent = true;
            };

            queryFirst.CollectionChanged += checkUpdatedFirstQueryEvent;

            bool notCalledSecondQueryEvent = true;
            void checkNotCalledSecondQueryEvent(object? sender, NotifyCollectionChangedEventArgs e)
            {
                notCalledSecondQueryEvent = false;
            };

            querySecond.CollectionChanged += checkNotCalledSecondQueryEvent;

            _container.Replace(firstBlog, firstBlog);

            _container.Save();

            await _container.Flush();

            queryFirst.Count.Should().Be(2);
            querySecond.Count.Should().Be(1);

            updatedFirstQueryEvent.Should().BeTrue();
            notCalledSecondQueryEvent.Should().BeTrue();

            queryFirst.CollectionChanged -= checkUpdatedFirstQueryEvent;
            querySecond.CollectionChanged -= checkNotCalledSecondQueryEvent;
        }
    }
}
