﻿using SqlKata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using static Dakata.DbUtils;

namespace Dakata
{
    public partial class BaseDal
    {
        public Query JoinTable(
            Func<string, Func<Join, Join>, Query> joiner, 
            string joinTableName, string joinTableColumnName, 
            string baseTableColumnName = null, 
            string baseTableName = null)
        {
            baseTableName = baseTableName ?? TableName;
            baseTableColumnName = baseTableColumnName ?? joinTableName + joinTableColumnName;
            return joiner(joinTableName,
                join => join.On(AddTablePrefix(joinTableColumnName, joinTableName), AddTablePrefix(baseTableColumnName, baseTableName))
            );
        }

        public Query InnerJoinTable(Query query, 
            string joinTableName, 
            string joinTableColumnName, 
            string baseTableColumnName = null, 
            string baseTableName = null)
        {
            return JoinTable((joinTableName2, join) => query.Join(joinTableName, join),
                joinTableName,
                joinTableColumnName,
                baseTableColumnName,
                baseTableName);
        }

        public Query InnerJoinTable<TJoinEntity>(Query query, 
            string joinTableColumnName, 
            string baseTableColumnName = null, 
            string baseTableName = null)
        {
            var joinTableName = GetTableName<TJoinEntity>();
            return InnerJoinTable(query, joinTableName, joinTableColumnName, baseTableColumnName, baseTableName);
        }

        public Query LeftJoinTable(Query query, 
            string joinTableName, 
            string joinTableColumnName, 
            string baseTableColumnName = null, 
            string baseTableName = null)
        {
            return JoinTable(query.LeftJoin, joinTableName, joinTableColumnName, baseTableColumnName, baseTableName);
        }

        public Query LeftJoinTable<TJoinEntity>(Query query, 
            string joinTableColumnName, 
            string baseTableColumnName = null, 
            string baseTableName = null)
        {
            var joinTableName = GetTableName<TJoinEntity>();
            return LeftJoinTable(query, joinTableName, joinTableColumnName, baseTableColumnName, baseTableName);
        }

        public Query Include(Query query,
            Type baseEntityType,
            Type joinEntityType,
            string selectPrefix,
            string joinTableColumnName,
            string baseTableColumnName = null,
            bool useLeftJoin = false)
        {
            var baseTableName = GetTableName(baseEntityType);
            var joinTableName = GetTableName(joinEntityType);
            query = useLeftJoin? 
                LeftJoinTable(query, 
                    joinTableName, joinTableColumnName,
                    baseTableColumnName, baseTableName) :
                InnerJoinTable(query, 
                    joinTableName, joinTableColumnName,
                    baseTableColumnName, baseTableName);

            var baseTableColumnSelections = 
                GetColumnSelections(tableName: baseTableName, entityType: baseEntityType);
            var baseTableColumnNames = baseTableColumnSelections
                    .Select(s => (selection: s, column: GetColumnExpressionFromSelectClause(s)))
                    .ToArray();
            var clauses = query.Clauses;
            var columnsInQuery = clauses.OfType<Column>().ToArray();
            var columnNamesInQuery = columnsInQuery
                .Select(x => GetColumnExpressionFromSelectClause(x.Name)).ToArray();

            baseTableColumnSelections = baseTableColumnNames
                .Where(c => !columnNamesInQuery
                            .Any(c2 => 
                                c2.Equals(c.column, StringComparison.InvariantCultureIgnoreCase))
                            )
                .Select(c => c.selection)
                .ToArray();
            
            if (baseTableColumnSelections.Any())
            {
                query.Select(baseTableColumnSelections);
            }
            var joinTableColumnSelections =
                GetColumnSelections(selectPrefix,
                    joinTableName,
                    joinEntityType);
            return query.Select(joinTableColumnSelections);
        }

        public Query Include<TJoinEntity>(Query query,
            string selectPrefix,
            string joinTableColumnName,
            string baseTableColumnName = null,
            bool useLeftJoin = false)
        {
            return Include(query, 
                baseEntityType: EntityType,
                typeof(TJoinEntity),
                selectPrefix,
                joinTableColumnName,
                baseTableColumnName,
                useLeftJoin);
        }

        public Query Include<TBaseEntity, 
            TJoinEntity, TJoinProperty>(Query query,
            string selectPrefix,
            Expression<Func<TJoinEntity, TJoinProperty>> joinProperty,
            Expression<Func<TBaseEntity, TJoinProperty>> baseProperty,
            bool useLeftJoin = false)
        {
            var joinTableColumnName = GetColumnName(joinProperty);
            var baseTableColumnName = GetColumnName(baseProperty);
            return Include(query,
                baseEntityType: typeof(TBaseEntity),
                joinEntityType: typeof(TJoinEntity),
                selectPrefix,
                joinTableColumnName,
                baseTableColumnName,
                useLeftJoin);
        }

        public Query Include<TBaseEntity, 
            TJoinEntity, 
            TJoinProperty>(Query query,
            Expression<Func<TBaseEntity, TJoinEntity>> includeProperty,
            Expression<Func<TJoinEntity, TJoinProperty>> joinProperty,
            Expression<Func<TBaseEntity, TJoinProperty>> baseProperty,
            bool useLeftJoin = false)
        {
            var selectPrefix = includeProperty.GetFullPropertyName();
            return Include(query,
                selectPrefix,
                joinProperty,
                baseProperty,
                useLeftJoin);
        }

        public Query IncludeList<TBaseEntity,
            TJoinEntity,
            TJoinProperty>(Query query,
            Expression<Func<TBaseEntity, List<TJoinEntity>>> includeProperty,
            Expression<Func<TJoinEntity, TJoinProperty>> joinProperty,
            Expression<Func<TBaseEntity, TJoinProperty>> baseProperty,
            bool useLeftJoin = false)
        {
            var selectPrefix = includeProperty.GetFullPropertyName();
            return Include(query,
                selectPrefix,
                joinProperty,
                baseProperty,
                useLeftJoin);
        }

        /// <summary>
        /// Left include an entity
        /// </summary>
        /// <typeparam name="TBaseEntity">The base entity type</typeparam>
        /// <typeparam name="TJoinEntity">The join entity type</typeparam>
        /// <param name="query">The query</param>
        /// <param name="selectPrefix">The select prefix</param>
        /// <param name="joinExpression">The expression to specify join properties,
        ///     should be something like 
        ///         () => baseEntity.JoinProperty = joinEntity.Poperty</param>
        /// <param name="useLeftJoin">Indicate whether to use left join</param>
        /// <returns></returns>
        public Query Include<TBaseEntity, TJoinEntity>(Query query,
            Expression<Func<TBaseEntity, TJoinEntity, bool>> joinExpression,
            string selectPrefix,
            bool useLeftJoin = false)
        {
            var (leftSideProps, rightSideProps) = new IncludeVisitor()
                .VisitEqualExpression(joinExpression.Body as BinaryExpression);
            var baseTableColumnName = GetColumnName(leftSideProps.Last());
            var joinTableColumnName = GetColumnName(rightSideProps.Last());
            return Include(query,
                baseEntityType: typeof(TBaseEntity),
                joinEntityType: typeof(TJoinEntity),
                selectPrefix: selectPrefix,
                joinTableColumnName: joinTableColumnName,
                baseTableColumnName: baseTableColumnName,
                useLeftJoin: useLeftJoin);
        }

        /// <summary>
        /// Left include an entity
        /// </summary>
        /// <typeparam name="TBaseEntity">The base entity type</typeparam>
        /// <typeparam name="TJoinEntity">The join entity type</typeparam>
        /// <param name="query">The query</param>
        /// <param name="joinExpression">The expression to specify the include property 
        ///     and join properties, should be something like 
        ///         () => baseEntity.IncludeProperty.JoinProperty = baseEntity.Poperty</param>
        /// <param name="selectPrefix">The select prefix</param>
        /// <param name="useLeftJoin">Indicate whether to use left join</param>
        /// <returns></returns>
        public Query Include<TBaseEntity, TJoinEntity>(Query query,
            Expression<Func<TBaseEntity, bool>> joinExpression,
            string selectPrefix = "",
            bool useLeftJoin = false)
        {
            var (leftSideProps, rightSideProps) = new IncludeVisitor()
                .VisitEqualExpression(joinExpression.Body as BinaryExpression);
            var baseTableColumnName = GetColumnName(leftSideProps.Last());
            var joinTableColumnName = GetColumnName(rightSideProps.Last());
            var selectPrefixFromPropertyName = rightSideProps[rightSideProps.Length - 2].Name;
            selectPrefix = selectPrefix.IsNullOrEmpty()? 
                selectPrefixFromPropertyName : 
                $"{selectPrefix}_{selectPrefixFromPropertyName}";
            return Include(query, 
                baseEntityType: typeof(TBaseEntity),
                joinEntityType: typeof(TJoinEntity), 
                selectPrefix: selectPrefix, 
                joinTableColumnName: joinTableColumnName, 
                baseTableColumnName: baseTableColumnName, 
                useLeftJoin: useLeftJoin);
        }

        private class IncludeVisitor: ExpressionVisitor
        {
            private Stack<PropertyInfo> LeftSideProperties { get; set; } = new Stack<PropertyInfo>();
            private Stack<PropertyInfo> RightSideProperties { get; set; } = new Stack<PropertyInfo>();
            private bool _isLeftSideParsingFinished = false;

            protected override Expression VisitMember(MemberExpression node)
            {
                var member = node.Member;
                if (member is PropertyInfo property)
                {
                    var propertyNames = _isLeftSideParsingFinished? RightSideProperties : LeftSideProperties;
                    propertyNames.Push(property);
                }
                return base.VisitMember(node);
            }

            public (PropertyInfo[] leftSideProperties, PropertyInfo[] rightSideProperties) 
                VisitEqualExpression(BinaryExpression node)
            {
                if (node.NodeType != ExpressionType.Equal)
                {
                    return default;
                }
                Visit(node.Left);
                _isLeftSideParsingFinished = true;
                Visit(node.Right);
                var propertyNames = (LeftSideProperties.ToArray(), RightSideProperties.ToArray());
                _isLeftSideParsingFinished = false;
                LeftSideProperties.Clear();
                RightSideProperties.Clear();
                return propertyNames;
            }
        }
    }

    public partial class BaseDal<TEntity>
    {
        public Query Include<TJoinEntity, TJoinProperty>(Query query,
            string selectPrefix,
            Expression<Func<TJoinEntity, TJoinProperty>> joinProperty,
            Expression<Func<TEntity, TJoinProperty>> baseProperty,
            bool useLeftJoin = false)
        {
            return base.Include
                (query, 
                selectPrefix, 
                joinProperty, 
                baseProperty, 
                useLeftJoin);
        }

        public Query Include<
            TJoinEntity,
            TJoinProperty>(Query query,
            Expression<Func<TEntity, TJoinEntity>> includeProperty,
            Expression<Func<TJoinEntity, TJoinProperty>> joinProperty,
            Expression<Func<TEntity, TJoinProperty>> baseProperty,
            bool useLeftJoin = false)
        {
            return base.Include(query,
                includeProperty,
                joinProperty,
                baseProperty,
                useLeftJoin);
        }

        public Query IncludeList<
            TJoinEntity,
            TJoinProperty>(Query query,
            Expression<Func<TEntity, List<TJoinEntity>>> includeProperty,
            Expression<Func<TJoinEntity, TJoinProperty>> joinProperty,
            Expression<Func<TEntity, TJoinProperty>> baseProperty,
            bool useLeftJoin = false)
        {
            return base.IncludeList(query,
                includeProperty,
                joinProperty,
                baseProperty,
                useLeftJoin);
        }

        public Query Include<TJoinEntity>(Query query,
            Expression<Func<TEntity, bool>> joinExpression,
            bool useLeftJoin = false)
        {
            return Include<TEntity, TJoinEntity>(query, joinExpression, useLeftJoin: useLeftJoin);
        }

        public Query Include<TJoinEntity>(Query query,
            Expression<Func<TEntity, TJoinEntity, bool>> joinExpression,
            string selectPrefix,
            bool useLeftJoin = false)
        {
            return Include<TEntity, TJoinEntity>(query, joinExpression, selectPrefix, useLeftJoin: useLeftJoin);
        }
    }
}
