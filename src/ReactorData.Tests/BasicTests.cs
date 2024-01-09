using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ReactorData;
using ReactorData.Tests.Models;

namespace ReactorData.Tests;

public class BasicTests
{
    IServiceProvider _services;
    IContainer _container;

    [SetUp]
    public void Setup()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddReactorData();
        _services = serviceCollection.BuildServiceProvider();

        _container = _services.GetRequiredService<IContainer>();
    }


    [Test]
    public async Task BasicOperationsOnEntity()
    {
        var todo = new Todo
        {
            Title = "My new blog"
        };

        _container.GetEntityStatus(todo).Should().Be(EntityStatus.Detached);

        _container.Add(todo);

        await _container.Flush();

        _container.GetEntityStatus(todo).Should().Be(EntityStatus.Added);

        _container.Set<Todo>().Single().Should().BeSameAs(todo);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(todo).Should().Be(EntityStatus.Attached);

        todo.Done = true;

        _container.Update(todo);

        await _container.Flush();

        _container.GetEntityStatus(todo).Should().Be(EntityStatus.Updated);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(todo).Should().Be(EntityStatus.Attached);

        _container.Delete(todo);

        await _container.Flush();

        _container.GetEntityStatus(todo).Should().Be(EntityStatus.Deleted);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(todo).Should().Be(EntityStatus.Detached);
    }
}