﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Takenet.Elephant.Sql.Mapping;

namespace Takenet.Elephant.Sql
{
    public class SqlSetMap<TKey, TItem> : MapStorageBase<TKey, TItem>, IItemSetMap<TKey, TItem>, IKeysMap<TKey, ISet<TItem>>
    {
        private readonly IsolationLevel _addIsolationLevel;

        #region Constructor

        public SqlSetMap(string connectionString, ITable table, IMapper<TKey> keyMapper, IMapper<TItem> valueMapper, IsolationLevel addIsolationLevel = IsolationLevel.ReadCommitted)
            : this(new SqlDatabaseDriver(), connectionString, table, keyMapper, valueMapper)
        {
            _addIsolationLevel = addIsolationLevel;
        }

        public SqlSetMap(IDatabaseDriver databaseDriver, string connectionString, ITable table, IMapper<TKey> keyMapper, IMapper<TItem> valueMapper, IsolationLevel addIsolationLevel = IsolationLevel.ReadCommitted)
            : base(databaseDriver, connectionString, table, keyMapper, valueMapper)
        {
            _addIsolationLevel = addIsolationLevel;
        }

        #endregion

        #region ISetMap<TKey, TItem> Members

        public async Task<bool> TryAddAsync(TKey key, ISet<TItem> value, bool overwrite = false)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (value == null) throw new ArgumentNullException(nameof(value));
            var keyColumnValues = GetKeyColumnValues(KeyMapper.GetColumnValues(key));

            var internalSet = value as InternalSet;
            if (internalSet != null)
            {
                return keyColumnValues.SequenceEqual(internalSet.MapKeyColumnValues) && overwrite;
            }

            using (var cancellationTokenSource = CreateCancellationTokenSource())
            {
                using (var connection = await GetConnectionAsync(cancellationTokenSource.Token).ConfigureAwait(false))
                {
                    if (!overwrite &&
                        await ContainsAsync(keyColumnValues, connection, cancellationTokenSource.Token))
                    {
                        return false;
                    }

                    using (var transaction = connection.BeginTransaction(_addIsolationLevel))
                    {
                        if (overwrite)
                        {
                            await
                                TryRemoveAsync(keyColumnValues, connection, cancellationTokenSource.Token, transaction)
                                    .ConfigureAwait(false);
                        }

                        var success = true;
                        var items = await value.AsEnumerableAsync().ConfigureAwait(false);
                        await items.ForEachAsync(
                            async item =>
                            {
                                if (!success) return;
                                var columnValues = GetColumnValues(key, item);
                                var itemKeyColumnValues = GetKeyColumnValues(columnValues);

                                using (
                                    var command = connection.CreateInsertWhereNotExistsCommand(DatabaseDriver, Table.Name,
                                        itemKeyColumnValues, columnValues))
                                {
                                    command.Transaction = transaction;
                                    success =
                                        await command.ExecuteNonQueryAsync(cancellationTokenSource.Token).ConfigureAwait(false) > 0;
                                }
                            },
                            cancellationTokenSource.Token);

                        if (success)
                        {
                            transaction.Commit();
                        }
                        else
                        {
                            transaction.Rollback();
                        }

                        return success;
                    }
                }
            }
        }

        public async Task<ISet<TItem>> GetValueOrDefaultAsync(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            using (var cancellationTokenSource = CreateCancellationTokenSource())
            {
                using (var connection = await GetConnectionAsync(cancellationTokenSource.Token).ConfigureAwait(false))
                {
                    var keyColumnValues = KeyMapper.GetColumnValues(key);
                    if (!await ContainsAsync(keyColumnValues, connection, cancellationTokenSource.Token))
                    {
                        return null;
                    }
                    return new InternalSet(ConnectionString, Table, Mapper, DatabaseDriver, keyColumnValues, QueryOrderByColumns, QueryOrderByAscending);
                }
            }
        }

        public async Task<bool> TryRemoveAsync(TKey key)
        {
            using (var cancellationTokenSource = CreateCancellationTokenSource())
            {
                using (var connection = await GetConnectionAsync(cancellationTokenSource.Token).ConfigureAwait(false))
                {
                    return await TryRemoveAsync(key, connection, cancellationTokenSource.Token);
                }
            }
        }

        public async Task<bool> ContainsKeyAsync(TKey key)
        {
            using (var cancellationTokenSource = CreateCancellationTokenSource())
            {
                using (var connection = await GetConnectionAsync(cancellationTokenSource.Token).ConfigureAwait(false))
                {
                    return await ContainsKeyAsync(key, connection, cancellationTokenSource.Token);
                }
            }
        }

        #endregion

        #region IItemSetMap<TKey, TItem> Members

        public virtual async Task<TItem> GetItemOrDefaultAsync(TKey key, TItem value)
        {
            var keyColumnValues = GetKeyColumnValues(GetColumnValues(key, value));
            var selectColumns = Table.Columns.Keys.ToArray();
            using (var cancellationTokenSource = CreateCancellationTokenSource())
            {
                return await new DbDataReaderAsyncEnumerable<TItem>(
                            GetConnectionAsync,
                            c => c.CreateSelectCommand(DatabaseDriver, Table.Name, keyColumnValues, selectColumns),
                            Mapper,
                            selectColumns)
                            .FirstOrDefaultAsync(cancellationTokenSource.Token);
            }
        }

        #endregion

        #region IKeysMap<TKey, ISet<TItem>> Members

        public virtual Task<IAsyncEnumerable<TKey>> GetKeysAsync()
        {
            var selectColumns = Table.KeyColumnsNames;
            return Task.FromResult<IAsyncEnumerable<TKey>>(
                new DbDataReaderAsyncEnumerable<TKey>(
                    GetConnectionAsync, 
                    c => c.CreateSelectCommand(DatabaseDriver, Table.Name, null, selectColumns), 
                    KeyMapper, 
                    selectColumns));
        }

        #endregion

        private class InternalSet : SqlSet<TItem>
        {
            private readonly string[] _queryOrderByColumns;
            private readonly bool _queryOrderByAscending;
            public IDictionary<string, object> MapKeyColumnValues { get; }

            public InternalSet(string connectionString, ITable table, IMapper<TItem> mapper, IDatabaseDriver databaseDriver, IDictionary<string, object> mapKeyColumnValues, string[] queryOrderByColumns, bool queryOrderByAscending) 
                : base(databaseDriver, connectionString, table, mapper)
            {
                _queryOrderByColumns = queryOrderByColumns;
                _queryOrderByAscending = queryOrderByAscending;
                MapKeyColumnValues = mapKeyColumnValues;
                SchemaChecked = true; // Avoid checking the table schema again
            }

            protected override IDictionary<string, object> GetColumnValues(TItem entity, bool emitDefaultValues = false)
            {
                return MapKeyColumnValues
                    .Union(base.GetColumnValues(entity, emitDefaultValues))
                    .ToDictionary(k => k.Key, k => k.Value);
            }

            public override Task<IAsyncEnumerable<TItem>> AsEnumerableAsync()
            {                                
                var selectColumns = Table.Columns.Keys.ToArray();                
                return Task.FromResult<IAsyncEnumerable<TItem>>(
                    new DbDataReaderAsyncEnumerable<TItem>(
                        GetConnectionAsync, 
                        c => c.CreateSelectCommand(DatabaseDriver, Table.Name, MapKeyColumnValues, selectColumns), 
                        Mapper, 
                        selectColumns));
            }

            public override async Task<long> GetLengthAsync()
            {
                using (var cancellationTokenSource = CreateCancellationTokenSource())
                {
                    using (var connection = await GetConnectionAsync(cancellationTokenSource.Token).ConfigureAwait(false))
                    {
                        using (var countCommand = connection.CreateSelectCountCommand(DatabaseDriver, Table.Name, MapKeyColumnValues))
                        {
                            var result = await countCommand.ExecuteScalarAsync(cancellationTokenSource.Token).ConfigureAwait(false);
                            // In postgre, it is a long; in sql server, a int32...
                            return Convert.ToInt64(result);
                        }
                    }
                }
            }

            protected override Task<QueryResult<TItem>> QueryAsync<TResult>(
                string filter, 
                string[] selectColumns, 
                int skip, 
                int take, 
                CancellationToken cancellationToken,
                string[] orderByColumns,
                bool orderByAscending = true,
                IDictionary<string, object> filterValues = null)
            {
                if (filterValues == null)
                {
                    filterValues = MapKeyColumnValues;
                }
                else
                {
                    filterValues = filterValues
                        .Union(MapKeyColumnValues)
                        .ToDictionary(k => k.Key, v => v.Value);
                }

                return base.QueryAsync<TResult>(filter, selectColumns, skip, take, cancellationToken, _queryOrderByColumns, _queryOrderByAscending, filterValues);
            }
        }
    }
}
