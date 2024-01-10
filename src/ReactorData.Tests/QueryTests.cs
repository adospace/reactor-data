using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ReactorData.Tests.Models;
using System.Collections.Specialized;

namespace ReactorData.Tests;

class QueryTests
{
    IServiceProvider _services;
    IModelContext _container;

    [SetUp]
    public void Setup()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddReactorData();
        _services = serviceCollection.BuildServiceProvider();
        _container = _services.GetRequiredService<IModelContext>();
    }

    [Test]
    public async Task TestQueryFunctions()
    {
        var todo = new Todo
        {
            Title = "Learn C#"
        };

        var query = _container.Query<Todo>(query => query.Where(_ => _.Done).OrderBy(_=>_.Title));

        _container.Add(todo);

        await _container.Flush();

        query.Count.Should().Be(0);

        var todo2 = new Todo
        {
            Title = "Learn Python",
            Done = true
        };

        bool addedEvent = false;
        void checkAddedEvent(object? sender, NotifyCollectionChangedEventArgs e) 
        {
            e.Action.Should().Be(NotifyCollectionChangedAction.Add);
            e.NewItems.Should().NotBeNull();
            e.NewItems![0].Should().BeSameAs(todo2);
            e.NewStartingIndex.Should().Be(0);
            e.OldItems.Should().BeNull();

            addedEvent = true;
        };

        query.CollectionChanged += checkAddedEvent;

        _container.Add(todo2);

        await _container.Flush();

        query.Count.Should().Be(1);

        addedEvent.Should().BeTrue();

        addedEvent = false;
        void checkAddedSecondEvent(object? sender, NotifyCollectionChangedEventArgs e)
        {
            e.Action.Should().Be(NotifyCollectionChangedAction.Add);
            e.NewItems.Should().NotBeNull();
            e.NewItems![0].Should().BeSameAs(todo);
            e.NewStartingIndex.Should().Be(0); //by default query order by key so here we have 0 as "Learn C#" is less than "Learn Python"
            e.OldItems.Should().BeNull();

            addedEvent = true;
        };

        query.CollectionChanged -= checkAddedEvent;
        query.CollectionChanged += checkAddedSecondEvent;

        todo.Done = true;

        _container.Update(todo);

        await _container.Flush();

        query.Count.Should().Be(2);

        addedEvent.Should().BeTrue();
    }
}
