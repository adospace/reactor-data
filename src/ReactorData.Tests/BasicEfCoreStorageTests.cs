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
        serviceCollection.AddReactorData<TestDbContext>(options => options.UseSqlite(_connection));

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

        //_container.Set<Blog>().Single().Should().BeSameAs(blog);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(blog).Should().Be(EntityStatus.Attached);

        blog.Title = "My new blog modified";

        var modifiedBlog = new Blog { Id = blog.Id, Title = "My new blog modified" };
        _container.Replace(blog, modifiedBlog);

        await _container.Flush();

        _container.GetEntityStatus(blog).Should().Be(EntityStatus.Detached);
        _container.GetEntityStatus(modifiedBlog).Should().Be(EntityStatus.Updated);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(modifiedBlog).Should().Be(EntityStatus.Attached);

        _container.Delete(modifiedBlog);

        await _container.Flush();

        _container.GetEntityStatus(modifiedBlog).Should().Be(EntityStatus.Deleted);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(modifiedBlog).Should().Be(EntityStatus.Detached);
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

        //_container.Set<Movie>().Single().Should().BeSameAs(movie);

        _container.Save();

        await _container.Flush();

        _container.GetEntityStatus(movie).Should().Be(EntityStatus.Attached);

    }

    [Test]
    public async Task BasicOperationsOnEntityWithManyToManyRelationshipUsingEfCoreStorage()
    {
        // Initialize some data
        {
            _container.Add(new Player { Name = "Player 1" }, new Game());
            _container.Save();
            await _container.Flush();
        }
        // Update the many-to-many relationship with existing models
        {
            var existingGame = _container.FindByKey<Game>(1)!;
            var existingPlayer = _container.FindByKey<Player>(1)!;
            _container.Add(new GamePlayer { GameId = existingGame.Id, PlayerId = existingPlayer.Id });
            _container.Save();
            await _container.Flush();
        }
        // Check the Sqlite database for the updated many-to-many relationship
        {
            var gamePlayers = new List<GamePlayerEntry>();
            await ExecuteSqliteReadCommand(
                "SELECT * FROM GamePlayers;",
                reader =>
                {
                    var gameId = reader.GetInt32(reader.GetOrdinal("GameId"));
                    var playerId = reader.GetInt32(reader.GetOrdinal("PlayerId"));
                    gamePlayers.Add(new GamePlayerEntry(gameId, playerId));
                });
            gamePlayers.Should().HaveCount(1);
            gamePlayers.Should().ContainSingle(e => e.GamesId == 1 && e.PlayerId == 1);
        }
    }
    private record GamePlayerEntry(int GamesId, int PlayerId);

    private async Task ExecuteSqliteReadCommand(string sqlCommand, Action<SqliteDataReader> onRead)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = sqlCommand;
        await using var reader = await command.ExecuteReaderAsync();
        ArgumentNullException.ThrowIfNull(reader);
        while (reader.Read())
        {
            onRead(reader);
        }
    }
}
