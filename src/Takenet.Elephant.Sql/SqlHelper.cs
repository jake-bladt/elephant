﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace Takenet.Elephant.Sql
{
    internal static class SqlHelper
    {        
        internal static string GetAndEqualsStatement(IDatabaseDriver databaseDriver, IDictionary<string, object> filterValues)
        {
            if (filterValues == null) return databaseDriver.GetSqlStatementTemplate(SqlStatement.OneEqualsOne);
            return GetAndEqualsStatement(databaseDriver, filterValues.Keys.ToArray());
        }

        internal static string GetAndEqualsStatement(IDatabaseDriver databaseDriver, string[] columns)
        {
            return GetAndEqualsStatement(databaseDriver, columns, columns);
        }

        internal static string GetAndEqualsStatement(IDatabaseDriver databaseDriver, string[] columns, string[] parameters)
        {
            return columns.Length == 0 ?
                databaseDriver.GetSqlStatementTemplate(SqlStatement.OneEqualsOne) : 
                GetSeparateEqualsStatement(databaseDriver, databaseDriver.GetSqlStatementTemplate(SqlStatement.And), columns, parameters);
        }

        internal static string GetCommaEqualsStatement(IDatabaseDriver databaseDriver, string[] columns)
        {
            return GetCommaEqualsStatement(databaseDriver, columns, columns);
        }

        internal static string GetCommaEqualsStatement(IDatabaseDriver databaseDriver, string[] columns, string[] parameters)
        {
            return GetSeparateEqualsStatement(databaseDriver, ",", columns, parameters);
        }

        internal static string GetSeparateEqualsStatement(IDatabaseDriver databaseDriver, string separator, string[] columns, string[] parameters)
        {
            return GetSeparateColumnsStatement(databaseDriver, separator, columns, parameters, GetEqualsStatement);
        }

        internal static string GetEqualsStatement(IDatabaseDriver databaseDriver, string column, string parameter)
        {
            return databaseDriver.GetSqlStatementTemplate(SqlStatement.QueryEquals).Format(
                new
                {
                    column = column.AsSqlIdentifier(),
                    value = parameter.AsSqlParameterName()
                });
        }

        internal static string GetCommaValueAsColumnStatement(IDatabaseDriver databaseDriver, string[] columns)
        {
            return GetValueAsColumnStatement(databaseDriver, ",", columns);
        }

        internal static string GetValueAsColumnStatement(IDatabaseDriver databaseDriver, string separator, string[] columns)
        {
            return GetValueAsColumnStatement(databaseDriver, separator, columns, columns);
        }

        internal static string GetValueAsColumnStatement(IDatabaseDriver databaseDriver, string separator, string[] columns, string[] parameters)
        {
            return GetSeparateColumnsStatement(databaseDriver, separator, columns, parameters, GetValueAsColumnStatement);
        }

        internal static string GetValueAsColumnStatement(IDatabaseDriver databaseDriver, string column, string parameter)
        {
            return databaseDriver.GetSqlStatementTemplate(SqlStatement.ValueAsColumn).Format(
                new
                {
                    value = parameter.AsSqlParameterName(),
                    column = column.AsSqlIdentifier()
                });
        }

        internal static string GetLiteralJoinConditionStatement(IDatabaseDriver databaseDriver, string[] columns, string sourceTableName, string targetTableName)
        {
            return GetSeparateColumnsStatement(
                databaseDriver,
                databaseDriver.GetSqlStatementTemplate(SqlStatement.And),
                columns,
                columns,
                (d, c, p) => databaseDriver.GetSqlStatementTemplate(SqlStatement.QueryEquals).Format(
                    new
                    {
                        column = $"{sourceTableName.AsSqlIdentifier()}.{c.AsSqlIdentifier()}",
                        value = $"{targetTableName.AsSqlIdentifier()}.{c.AsSqlIdentifier()}",
                    }));

        }

        internal static string GetSeparateColumnsStatement(IDatabaseDriver databaseDriver, string separator, string[] columns, string[] parameters, Func<IDatabaseDriver, string, string, string> statement)
        {
            if (columns.Length == 0)
            {
                throw new ArgumentException("The columns are empty", nameof(columns));
            }

            if (parameters.Length == 0)
            {
                throw new ArgumentException("The parameters are empty", nameof(parameters));
            }

            var filter = new StringBuilder();

            for (int i = 0; i < columns.Length; i++)
            {
                var column = columns[i];
                var parameter = parameters[i];

                filter.Append(
                    statement(databaseDriver, column, parameter));

                if (i + 1 < columns.Length)
                {
                    filter.AppendFormat(" {0} ", separator);
                }
            }

            return filter.ToString();
        }

        internal static string TranslateToSqlWhereClause<TEntity>(IDatabaseDriver databaseDriver, Expression<Func<TEntity, bool>> where, IDictionary<string, string> parameterReplacementDictionary = null)
        {            
            if (where == null) return databaseDriver.GetSqlStatementTemplate(SqlStatement.OneEqualsOne);
            var translator = new SqlExpressionTranslator(databaseDriver, parameterReplacementDictionary);
            return translator.GetStatement(where);
        }
    }
}
