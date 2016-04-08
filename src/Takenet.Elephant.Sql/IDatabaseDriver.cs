﻿using System;
using System.Data;
using System.Data.Common;

namespace Takenet.Elephant.Sql
{
    public interface IDatabaseDriver
    {
        TimeSpan Timeout { get; }

        DbConnection CreateConnection(string connectionString);

        string GetSqlStatementTemplate(SqlStatement sqlStatement);

        string GetSqlTypeName(DbType dbType);
    }

    public enum SqlStatement
    {
        AlterTableAddColumn,
        And,
        ColumnDefinition,
        CreateTable,
        Delete,
        DeleteAndInsertWhereNotExists,
        Equal,
        Exists,
        GetTableColumns,
        IdentityColumnDefinition,
        In,
        Insert,
        InsertWhereNotExists,
        NullableColumnDefinition,
        Or,
        PrimaryKeyConstraintDefinition,
        QueryEquals,
        QueryGreatherThen,
        QueryLessThen,
        Select,
        SelectCount,
        SelectSkipTake,
        SelectTop1,
        TableExists,
        Update,
        Merge,
        OneEqualsOne,
        OneEqualsZero,
        DummyEqualsZero,
        ValueAsColumn,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Like
    }
}
