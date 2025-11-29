using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace Dime.Repositories
{
    internal static class DataReaderExtensions
    {
        internal static List<T> GetRecords<T>(this IDataReader reader)
        {
            List<T> result = [];
            while (reader.Read())
            {
                T t = (T)typeof(T).GetConstructor(Type.EmptyTypes).Invoke([]);
                PropertyInfo[] props = t.GetType().GetProperties();
                object[] indexer = null;
                foreach (PropertyInfo p in props)
                {
                    if (reader.HasColumn(p.Name) && reader[p.Name].GetType() != typeof(DBNull))
                        p.SetValue(t, reader[p.Name], indexer);
                }

                result.Add(t);
            }

            return result;
        }

        public static bool HasColumn(this IDataRecord dr, string columnName)
        {
            for (int i = 0; i < dr.FieldCount; i++)
            {
                if (dr.GetName(i).Equals(columnName, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}