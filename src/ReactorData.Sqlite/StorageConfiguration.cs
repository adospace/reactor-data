using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData.Sqlite;

public class StorageConfiguration
{
    private readonly Dictionary<Type, StorageModelConfiguration> _models = [];

    public SqliteConnection Connection { get; }

    internal StorageConfiguration(SqliteConnection connection)
    {
        Connection = connection;
    }

    internal IReadOnlyDictionary<Type, StorageModelConfiguration> Models => _models;

    public StorageConfiguration Model<T>(string? tableName = null, string? keyPropertyName = null) where T : class, IEntity
    {
        var typeOfT = typeof(T);

        var properties = typeOfT.GetProperties();

        PropertyInfo? keyProperty = null;

        if (keyPropertyName != null)
        {
            keyProperty ??= properties
                .FirstOrDefault(p => p.Name == keyPropertyName);
        }
        else
        {
            

            keyProperty = properties
                .FirstOrDefault(p => p.GetCustomAttribute(typeof(KeyAttribute)) != null);

            keyProperty ??= properties
                .FirstOrDefault(p => p.Name == "Id");

            keyProperty ??= properties
                .FirstOrDefault(p => p.Name == "Key");
        }

        keyProperty = keyProperty ?? throw new InvalidOperationException($"Unable to find Key property on model {typeOfT.Name}");

        keyPropertyName = keyProperty.Name;
        var keyPropertyType = keyProperty.PropertyType;

        _models[typeof(T)] = new StorageModelConfiguration(tableName ?? typeof(T).Name, keyPropertyName, keyPropertyType, keyProperty);
        return this;
    }
}

class StorageModelConfiguration(string tableName, string keyPropertyName, Type keyPropertyType, PropertyInfo keyPropertyInfo)
{
    public string TableName { get; } = tableName;
    
    public string KeyPropertyName { get; } = keyPropertyName;

    public Type KeyPropertyType { get; } = keyPropertyType;

    public PropertyInfo KeyPropertyInfo { get; } = keyPropertyInfo;
}