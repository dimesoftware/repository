using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Dime.Repositories
{
    internal class LinqOrderHelper<TSource>
    {
        internal LinqOrderHelper(string methodName, string propertyName)
        {
            Method = methodName;
            ParentParameterExpression = Expression.Parameter(typeof(TSource), "x");
            MemberExpression = SetMember(Parse(propertyName));
        }

        private string Method { get; }
        private ParameterExpression ParentParameterExpression { get; }
        private MemberExpression MemberExpression { get; }

        internal IOrderedQueryable<TSource> GetAsQueryable(IEnumerable<TSource> query)
        {
            LambdaExpression selector = Expression.Lambda(MemberExpression, ParentParameterExpression);
            MethodInfo methodInfo = GetMethodInfo(Method, MemberExpression.Type);

            IOrderedQueryable<TSource> newQuery = (IOrderedQueryable<TSource>)methodInfo.Invoke(methodInfo, [query, selector]);
            return newQuery;
        }

        internal IOrderedEnumerable<TSource> GetAsEnumerable(IEnumerable<TSource> query)
        {
            LambdaExpression selector = Expression.Lambda(MemberExpression, ParentParameterExpression);
            MethodInfo methodInfo = GetMethodInfo(Method, MemberExpression.Type);

            IOrderedEnumerable<TSource> newQuery = (IOrderedEnumerable<TSource>)methodInfo.Invoke(methodInfo, [query, selector]);
            return newQuery;
        }

        private MemberExpression SetMember(IOrderedEnumerable<KeyValuePair<int, string>> orderedList)
        {
            MemberExpression memberExpression = default;
            for (int i = 0; i < orderedList.Count(); i++)
            {
                string dataIndex = orderedList.ElementAt(i).Value;

                memberExpression = i == 0 ?
                    Expression.PropertyOrField(ParentParameterExpression, dataIndex) :
                    Expression.PropertyOrField(memberExpression, dataIndex);
            }

            return memberExpression;
        }

        private static MethodInfo GetMethodInfo(string methodName, Type type)
        {
            Type enumarableType = typeof(Queryable);
            MethodInfo method = enumarableType.GetMethods()
                 .Where(m => m.Name == methodName && m.IsGenericMethodDefinition)
                 .Where(m =>
                 {
                     List<ParameterInfo> parameters = [.. m.GetParameters()];
                     return parameters.Count == 2;
                 }).Single();

            MethodInfo genericMethod = method.MakeGenericMethod(typeof(TSource), type);

            return genericMethod;
        }

        private static IOrderedEnumerable<KeyValuePair<int, string>> Parse(string propertyName)
        {
            int c = 1;
            IDictionary<int, string> properties = new Dictionary<int, string>();
            propertyName.Split('.').ToList().ForEach(x => { properties.Add(c, x); c++; });

            IOrderedEnumerable<KeyValuePair<int, string>> orderedList = properties.OrderBy(x => x.Key);
            return orderedList;
        }
    }
}