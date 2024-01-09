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
                var command = connection.CreateCommand();

                var isKeyInt = modelConfiguration.Value.KeyPropertyType == typeof(int);
                var keyTypeName = GetSqliteTypeFor(modelConfiguration.Value.KeyPropertyType);

                command.CommandText = $$"""
                CREATE TABLE IF NOT EXISTS {{modelConfiguration.Value.TableName}} (ID {{(isKeyInt ? "INTEGER PRIMARY KEY" : keyTypeName)}}, MODEL TEXT)
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
            type == typeof(DateTimeOffset)
            )
        {
            return "TEXT";
        }
        else if (type == typeof(byte[]))
        {
            return "BLOB";
        }

        throw new NotImplementedException();
    }

    public async Task<IEnumerable<IEntity>> Load<TEntity>(Func<IQueryable<TEntity>, IQueryable<TEntity>>? queryFunction = null) where TEntity : class, IEntity
    {
        using var connectionHandler = new ConnectionHandler(this);
        var connection = await connectionHandler.GetConnection();

        await Initialize(connection);

        var command = connection.EnsureNotNull().CreateCommand();

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
            //object? key = null;

            //if (modelConfiguration.KeyPropertyType == typeof(int))
            //{
            //    key = reader.GetInt32(0);
            //}
            //else if (modelConfiguration.KeyPropertyType == typeof(string))
            //{
            //    key = reader.GetString(0);
            //}
            //else if (modelConfiguration.KeyPropertyType == typeof(Guid))
            //{
            //    key = Guid.Parse(reader.GetString(0));
            //}
            //else
            //{
            //    //todo...
            //    throw new NotSupportedException();
            //}

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
            command.Parameters.Clear();

            switch (operation)
            {
                case StorageInsert storageInsert:
                    {
                        var entityType = storageInsert.Entity.GetType();
                        if (!_configuration.EnsureNotNull().Models.TryGetValue(entityType, out var modelConfiguration))
                        {
                            throw new InvalidOperationException($"Missing model configuration for {entityType.Name}");
                        }

                        //insert into MyTable (Column1, Column2) Values (Val1, Val2) Returning RowId;
                        if (modelConfiguration.KeyPropertyType == typeof(int))
                        {
                            command.CommandText = $$"""
                                INSERT INTO {{modelConfiguration.TableName}} (MODEL) VALUES ($json) RETURNING ROWID
                                """;
                            command.Parameters.AddWithValue("$json", "{}");

                            var key = await command.ExecuteScalarAsync();

                            modelConfiguration.KeyPropertyInfo.SetValue(storageInsert.Entity, Convert.ChangeType(key, modelConfiguration.KeyPropertyType));

                            var json = System.Text.Json.JsonSerializer.Serialize(storageInsert.Entity, entityType);

                            command.CommandText = $$"""
                                UPDATE {{modelConfiguration.TableName}} SET MODEL = $json WHERE ID = $id
                                """;
                            command.Parameters.Clear();
                            command.Parameters.AddWithValue("$id", storageInsert.Entity.GetKey().EnsureNotNull());
                            command.Parameters.AddWithValue("$json", json);

                            await command.ExecuteNonQueryAsync();
                        }
                        else
                        {
                            var json = System.Text.Json.JsonSerializer.Serialize(storageInsert.Entity, entityType);

                            command.CommandText = $$"""
                                INSERT INTO {{modelConfiguration.TableName}} (ID, MODEL) VALUES ($id, $json)
                                """;
                            command.Parameters.AddWithValue("$id", storageInsert.Entity.GetKey().EnsureNotNull());
                            command.Parameters.AddWithValue("$json", json);

                            await command.ExecuteNonQueryAsync();
                        }
                    }
                    break;
                case StorageUpdate storageUpdate:
                    {
                        var entityType = storageUpdate.Entity.GetType();
                        if (!_configuration.EnsureNotNull().Models.TryGetValue(entityType, out var modelConfiguration))
                        {
                            throw new InvalidOperationException($"Missing model configuration for {entityType.Name}");
                        }
                        var json = System.Text.Json.JsonSerializer.Serialize(storageUpdate.Entity, entityType);

                        command.CommandText = $$"""
                                UPDATE {{modelConfiguration.TableName}} SET MODEL = $json WHERE ID = $id
                                """;
                        command.Parameters.AddWithValue("$id", storageUpdate.Entity.GetKey().EnsureNotNull());
                        command.Parameters.AddWithValue("$json", json);

                        await command.ExecuteNonQueryAsync();
                    }
                    break;
                case StorageDelete storageDelete:
                    {
                        var entityType = storageDelete.Entity.GetType();
                        if (!_configuration.EnsureNotNull().Models.TryGetValue(entityType, out var modelConfiguration))
                        {
                            throw new InvalidOperationException($"Missing model configuration for {entityType.Name}");
                        }

                        command.CommandText = $$"""
                                DELETE FROM {{modelConfiguration.TableName}} WHERE ID = $id
                                """;
                        command.Parameters.AddWithValue("$id", storageDelete.Entity.GetKey().EnsureNotNull());

                        await command.ExecuteNonQueryAsync();
                    }
                    break;
            }
        }
    }
}
