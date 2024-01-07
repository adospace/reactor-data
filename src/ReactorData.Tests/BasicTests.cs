using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ReactorData;

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

    [Model]
    class Blog : IEntity
    {
        public int Id { get; set; }

        public required string Title { get; set; }
    }

    [Test]
    public async Task BasicOperationsOnEntity()
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
}