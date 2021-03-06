﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Take.Elephant.Redis
{
    /// <summary>
    /// Implements the <see cref="IMap{TKey,TValue}"/> interface using Redis hash data structure.
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public class RedisHashMap<TKey, TValue> : MapBase<TKey, TValue>, IPropertyMap<TKey, TValue>
    {
        private readonly IRedisDictionaryConverter<TValue> _dictionaryConverter;
        private readonly HashSet<string> _propertiesNameHashSet;

        #region Constructor

        public RedisHashMap(string mapName, IRedisDictionaryConverter<TValue> dictionaryConverter, string configuration, int db = 0, CommandFlags readFlags = CommandFlags.None, CommandFlags writeFlags = CommandFlags.None)
            : this(mapName, dictionaryConverter, StackExchange.Redis.ConnectionMultiplexer.Connect(configuration), db, readFlags, writeFlags)
        {
            
        }

        public RedisHashMap(string mapName, IRedisDictionaryConverter<TValue> dictionaryConverter, IConnectionMultiplexer connectionMultiplexer, int db = 0, CommandFlags readFlags = CommandFlags.None, CommandFlags writeFlags = CommandFlags.None)
            : base(mapName, connectionMultiplexer, db, readFlags, writeFlags)
        {
            _dictionaryConverter = dictionaryConverter;
            _propertiesNameHashSet = new HashSet<string>(_dictionaryConverter.Properties);
        }

        #endregion

        #region IMap<TKey,TValue> Members

        public override async Task<bool> TryAddAsync(TKey key,
            TValue value,
            bool overwrite = false,
            CancellationToken cancellationToken = default)
        {
            if (!overwrite &&
                await ContainsKeyAsync(key))
            {
                return false;
            }

            await MergeAsync(key, value);
            return true;
        }

        public override async Task<TValue> GetValueOrDefaultAsync(TKey key,
            CancellationToken cancellationToken = default)
        {
            var database = GetDatabase();
            var hashEntries = await database.HashGetAllAsync(GetRedisKey(key), ReadFlags);
            if (hashEntries != null &&
                hashEntries.Length > 0)
            {
                var entriesDictionary = hashEntries.ToDictionary(t => (string)t.Name, t => t.Value);
                return _dictionaryConverter.FromDictionary(entriesDictionary);
            }

            return default(TValue);
        }

        public override Task<bool> TryRemoveAsync(TKey key, CancellationToken cancellationToken = default)
        {
            var database = GetDatabase();
            return database.KeyDeleteAsync(GetRedisKey(key), WriteFlags);
        }

        public override Task<bool> ContainsKeyAsync(TKey key, CancellationToken cancellationToken = default)
        {
            var database = GetDatabase();
            return database.KeyExistsAsync(GetRedisKey(key), ReadFlags);
        }

        #endregion

        #region IPropertyMap<TKey,TValue> Members

        public virtual async Task SetPropertyValueAsync<TProperty>(TKey key, string propertyName, TProperty propertyValue)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));
            if (!_propertiesNameHashSet.Contains(propertyName)) throw new ArgumentException($"The property '{propertyName}' is invalid");
            if (propertyValue == null) throw new ArgumentNullException(nameof(propertyValue));
            
            var database = GetDatabase();

            await database.HashSetAsync(GetRedisKey(key), propertyName, propertyValue.ToRedisValue(), When.Always, WriteFlags);
        }

        public virtual Task MergeAsync(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (value == null) throw new ArgumentNullException(nameof(value));

            var database = GetDatabase();

            var dictionary = _dictionaryConverter.ToDictionary(value);
            var hashEntries = dictionary
                .Select(i => new HashEntry(i.Key, i.Value))
                .ToArray();

            return hashEntries.Any() ? 
                database.HashSetAsync(GetRedisKey(key), hashEntries, WriteFlags) : 
                TaskUtil.CompletedTask;
        }

        public virtual async Task<TProperty> GetPropertyValueOrDefaultAsync<TProperty>(TKey key, string propertyName)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (propertyName == null) throw new ArgumentNullException(nameof(propertyName));
            if (!_propertiesNameHashSet.Contains(propertyName)) throw new ArgumentException($"The property '{propertyName}' is invalid");

            var database = GetDatabase();

            var redisValue = await database.HashGetAsync(GetRedisKey(key), propertyName, ReadFlags);
            return !redisValue.IsNull ? 
                redisValue.Cast<TProperty>() : 
                default(TProperty);
        }

        #endregion            
    }
}
