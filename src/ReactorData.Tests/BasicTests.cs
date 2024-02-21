using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ReactorData;
using ReactorData.Tests.Models;

namespace ReactorData.Tests;

public class BasicTests
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

        //_container.Set<Todo>().Single().Should().BeSameAs(todo);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(todo).Should().Be(EntityStatus.Attached);

        var modifiedTodo = new Todo { Title = todo.Title, Done = true };
        _container.Replace(todo, modifiedTodo);

        await _container.Flush();

        _container.GetEntityStatus(modifiedTodo).Should().Be(EntityStatus.Updated);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(modifiedTodo).Should().Be(EntityStatus.Attached);

        _container.Delete(modifiedTodo);

        await _container.Flush();

        _container.GetEntityStatus(modifiedTodo).Should().Be(EntityStatus.Deleted);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(modifiedTodo).Should().Be(EntityStatus.Detached);
    }
}