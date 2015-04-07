using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Takenet.SimplePersistence.Sql.Mapping;

namespace Takenet.SimplePersistence.Sql
{
    public abstract class MapStorageBase<TKey, TValue> : StorageBase<TValue>
    {
        protected MapStorageBase(ITable table, string connectionString) 
            : base(table, connectionString)
        {

        }

        protected abstract IMapper<TKey> KeyMapper { get; }

        protected virtual IDictionary<string, object> GetColumnValues(TKey key, TValue value)
        {
            return KeyMapper
                .GetColumnValues(key)
                .Concat(GetColumnValues(value))
                .ToDictionary(t => t.Key, t => t.Value);
        }

        protected virtual async Task<bool> TryRemoveAsync(TKey key, SqlConnection connection, CancellationToken cancellationToken, SqlTransaction sqlTransaction = null)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var keyColumnValues = KeyMapper.GetColumnValues(key);
            return await TryRemoveAsync(keyColumnValues, connection, cancellationToken, sqlTransaction);
            
        }

        protected virtual async Task<bool> ContainsKeyAsync(TKey key, SqlConnection connection, CancellationToken cancellationToken)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var keyColumnValues = KeyMapper.GetColumnValues(key);
            return await ContainsAsync(keyColumnValues, connection, cancellationToken);            
        }
    }
}