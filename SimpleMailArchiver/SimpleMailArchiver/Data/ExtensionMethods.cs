using System.Linq.Expressions;

namespace SimpleMailArchiver.Data;

public static class ExtensionMethods
{
    public static IQueryable<TEntity> OrderBy<TEntity>(this IQueryable<TEntity> source, string orderByProperty,
        bool asc)
    {
        var command = asc ? "OrderBy" : "OrderByDescending";
        var type = typeof(TEntity);
        var property = type.GetProperty(orderByProperty) ?? throw new NullReferenceException();
        var parameter = Expression.Parameter(type, "p");
        var propertyAccess = Expression.MakeMemberAccess(parameter, property);
        var orderByExpression = Expression.Lambda(propertyAccess, parameter);
        var resultExpression = Expression.Call(typeof(Queryable), command, [type, property.PropertyType],
            source.Expression, Expression.Quote(orderByExpression));
        return source.Provider.CreateQuery<TEntity>(resultExpression);
    }
}