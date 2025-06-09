using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;


namespace ReactorData.Sqlite.Implementation;


class Storage : IStorage
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string? _connectionString;
    private SqliteConnection? _connection;
    private readonly Action<StorageConfiguration>? _configureAction;
    private readonly SemaphoreSlim _semaphore = new(1);
    private bool _initialized;
    private StorageConfiguration? _configuration;

    class ConnectionHandler : IDisposable
    {
        readonly SqliteConnection? _currentConnection;
        private readonly Storage _storage;

        public ConnectionHandler(Storage storage)
        {
            if (storage._connection == null)
            {
                _currentConnection = new SqliteConnection(storage._connectionString.EnsureNotNull());
            }
            _storage = storage;
        }

        public async Task<SqliteConnection> GetConnection()
        {
            var connection = (_currentConnection ?? _storage._connection).EnsureNotNull();

            if (connection.State == System.Data.ConnectionState.Closed)
            {
                await connection.OpenAsync();
            }

            return connection;
        }

        public void Dispose()
        {
            _currentConnection?.Dispose();
        }
    }

    public Storage(IServiceProvider serviceProvider, string connectionString, Action<StorageConfiguration>? configureAction = null)
    {
        _serviceProvider = serviceProvider;
        _connectionString = connectionString;
        _configureAction = configureAction;
    }

    public Storage(IServiceProvider serviceProvider, SqliteConnection connection, Action<StorageConfiguration>? configureAction = null)
    {
        _serviceProvider = serviceProvider;
        _connection = connection;
        _configureAction = configureAction;
    }

    private async ValueTask Initialize(SqliteConnection connection)
    {
        if (_initialized)
        {
            return;
        }
            
        try
        {
            _semaphore.Wait();

            if (_initialized)
            {
                return;
            }

            _configuration = new StorageConfiguration(connection);

            _configureAction?.Invoke(_configuration);

            foreach (var modelConfiguration in _configuration.Models)
            {
                using var command = connection.CreateCommand();

                var keyTypeName = GetSqliteTypeFor(modelConfiguration.Value.KeyPropertyType);

                command.CommandText = $$"""
                CREATE TABLE IF NOT EXISTS {{modelConfiguration.Value.TableName}} (ID {{keyTypeName}} PRIMARY KEY, MODEL TEXT)
                """;

                await command.ExecuteNonQueryAsync();
            }

            _initialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    static string GetSqliteTypeFor(Type type)
    {
        //from https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/types
        if (type == typeof(int) ||
            type == typeof(short) ||
            type == typeof(long) ||
            type == typeof(sbyte) ||
            type == typeof(ushort) ||
            type == typeof(uint) ||
            type == typeof(bool) ||
            type == typeof(byte))
        {
            return "INTEGER";
        }
        else if (type == typeof(string) ||
            type == typeof(TimeOnly) ||
            type == typeof(TimeSpan) ||
            type == typeof(decimal) ||
            type == typeof(char) ||
            type == typeof(DateOnly) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(Guid)
            )
        {
            return "TEXT";
        }
        else if (type == typeof(byte[]))
        {
            return "BLOB";
        }

        throw new NotImplementedException($"Unable to get the Sqlite type for type: {type}");
    }

    public async Task<IEnumerable<IEntity>> Load<TEntity>(Func<IQueryable<TEntity>, IQueryable<TEntity>>? queryFunction = null) where TEntity : class, IEntity
    {
        using var connectionHandler = new ConnectionHandler(this);
        var connection = await connectionHandler.GetConnection();

        await Initialize(connection);

        using var command = connection.EnsureNotNull().CreateCommand();

        var entityType = typeof(TEntity);
        if (!_configuration.EnsureNotNull().Models.TryGetValue(entityType, out var modelConfiguration))
        {
            throw new InvalidOperationException($"Missing model configuration for {entityType.Name}");
        }

        command.CommandText = $$"""
            SELECT * FROM {{modelConfiguration.TableName}}
            """;

        var listOfLoadedEntities = new List<TEntity>();

        using var reader = command.ExecuteReader();

        while (await reader.ReadAsync())
        {
            var json = reader.GetString(1);

            var loadedEntity = (TEntity)System.Text.Json.JsonSerializer.Deserialize(json, typeof(TEntity)).EnsureNotNull();

            listOfLoadedEntities.Add(loadedEntity);
        }

        if (queryFunction != null)
        {
            return queryFunction(listOfLoadedEntities.AsQueryable());
        }

        return listOfLoadedEntities;
    }

    public async Task Save(IEnumerable<StorageOperation> operations)
    {
        using var connectionHandler = new ConnectionHandler(this);
        var connection = await connectionHandler.GetConnection();

        await Initialize(connection);

        using var command = connection.EnsureNotNull().CreateCommand();

        foreach (var operation in operations)
        {
            switch (operation)
            {
                case StorageAdd storageInsert:
                    {
                        foreach (var entity in storageInsert.Entities)
                        {
                            await Insert(command, (IEntity)entity);
                        }
                    }
                    break;
                case StorageUpdate storageUpdate:
                    {
                        foreach (var entity in storageUpdate.Entities)
                        {
                            await Update(command, (IEntity)entity);
                        }
                    }
                    break;
                case StorageDelete storageDelete:
                    {
                        foreach (var entity in storageDelete.Entities)
                        {
                            await Delete(command, (IEntity)entity);
                        }
                    }
                    break;
            }
        }
    }

    private async Task Delete(SqliteCommand command, IEntity entity)
    {
        var entityType = entity.GetType();
        if (!_configuration.EnsureNotNull().Models.TryGetValue(entityType, out var modelConfiguration))
        {
            throw new InvalidOperationException($"Missing model configuration for {entityType.Name}");
        }

        command.CommandText = $$"""
                                DELETE FROM {{modelConfiguration.TableName}} WHERE ID = $id
                                """;
        command.Parameters.Clear();
        command.Parameters.AddWithValue("$id", entity.GetKey().EnsureNotNull());

        await command.ExecuteNonQueryAsync();
    }

    private async Task Update(SqliteCommand command, IEntity entity)
    {
        var entityType = entity.GetType();
        if (!_configuration.EnsureNotNull().Models.TryGetValue(entityType, out var modelConfiguration))
        {
            throw new InvalidOperationException($"Missing model configuration for {entityType.Name}");
        }

        var json = System.Text.Json.JsonSerializer.Serialize(entity, entityType);

        command.CommandText = $$"""
                                UPDATE {{modelConfiguration.TableName}} SET MODEL = $json WHERE ID = $id
                                """;
        command.Parameters.Clear();
        command.Parameters.AddWithValue("$id", entity.GetKey().EnsureNotNull());
        command.Parameters.AddWithValue("$json", json);

        await command.ExecuteNonQueryAsync();
    }

    private async Task Insert(SqliteCommand command, IEntity entity)
    {
        var entityType = entity.GetType();
        if (!_configuration.EnsureNotNull().Models.TryGetValue(entityType, out var modelConfiguration))
        {
            throw new InvalidOperationException($"Missing model configuration for {entityType.Name}");
        }

        var keyValue = entity.GetKey().EnsureNotNull();
        if (modelConfiguration.KeyPropertyType == typeof(int) &&
            (int)keyValue == 0)
        {
            command.CommandText = $$"""
                                    INSERT INTO {{modelConfiguration.TableName}} (MODEL) VALUES ($json) RETURNING ROWID
                                    """;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$json", "{}");

            var key = await command.ExecuteScalarAsync();

            modelConfiguration.KeyPropertyInfo.SetValue(entity, Convert.ChangeType(key, modelConfiguration.KeyPropertyType));

            var json = System.Text.Json.JsonSerializer.Serialize(entity, entityType);

            command.CommandText = $$"""
                                    UPDATE {{modelConfiguration.TableName}} SET MODEL = $json WHERE ID = $id
                                    """;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$id", keyValue);
            command.Parameters.AddWithValue("$json", json);

            await command.ExecuteNonQueryAsync();
        }
        else
        {
            var json = System.Text.Json.JsonSerializer.Serialize(entity, entityType);

            command.CommandText = $$"""
                                    INSERT INTO {{modelConfiguration.TableName}} (ID, MODEL) VALUES ($id, $json)
                                    """;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$id", keyValue);
            command.Parameters.AddWithValue("$json", json);

            await command.ExecuteNonQueryAsync();
        }
    }
}
