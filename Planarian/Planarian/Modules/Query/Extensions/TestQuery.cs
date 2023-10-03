// // Define a Delegate
//
// using Planarian.Model.Database.Entities.RidgeWalker;
// using Planarian.Modules.Caves.Repositories;
// using Planarian.Modules.Query.Models;
//
// namespace Planarian.Modules.Query.Extensions;
//
// public delegate IQueryable<TEntity> QueryModifier<TEntity>(IQueryable<TEntity> query, object value);
//
// // Define a Field Condition Class
// public class FieldCondition<TEntity>
// {
//     public string FieldName { get; }
//     public QueryModifier<TEntity> Modifier { get; }
//
//     public FieldCondition(string fieldName, QueryModifier<TEntity> modifier)
//     {
//         FieldName = fieldName;
//         Modifier = modifier;
//     }
// }
//
// public static class QuerySearch
// {
//     private static readonly Dictionary<(Type EntityType, string FieldName), object> FieldConditions =
//         new()
//         {
//             {
//                 (typeof(Cave), nameof(CaveQuery.StateId)),
//                 new FieldCondition<Cave>(nameof(CaveQuery.StateId), (query, value) => query.Where(e => e.StateId == value))
//             }
//         };
//
//     public static IQueryable<TEntity> ApplyCondition<TEntity>(IQueryable<TEntity> query, QueryCondition condition)
//     {
//         if (FieldConditions.TryGetValue((typeof(TEntity), condition.Field), out var obj))
//         {
//             var fieldCondition = (FieldCondition<TEntity>)obj; // Cast to the correct type
//             return fieldCondition.Modifier(query, condition.Value);
//         }
//
//         // Handle the case where no field condition is found, e.g., throw an exception
//         throw new InvalidOperationException(
//             $"No field condition found for type {typeof(TEntity).Name} and field {condition.Field}.");
//     }
// }

