using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CsTsApi
{
    static class Reflection
    {
        public static Dictionary<string, Type> KnownTypes = new Dictionary<string, Type>();

        public static IEnumerable<Attribute> GetAttributes(this MemberInfo m, string typeName)
        {
            var result = m.GetCustomAttributes().Where(a => a.GetType().FullName == typeName).ToList();
            if (result.Count > 0)
                KnownTypes[result[0].GetType().FullName] = result[0].GetType();
            return result;
        }

        public static IEnumerable<Attribute> GetAttributesByInterface(this MemberInfo m, string interfaceName)
        {
            foreach (var attr in m.GetCustomAttributes())
            {
                var ifaces = attr.GetType().GetInterfaces().Where(i => i.FullName == interfaceName).ToList();
                if (ifaces.Count > 0)
                {
                    KnownTypes[ifaces[0].FullName] = ifaces[0];
                    yield return attr;
                }
            }
        }

        public static object ReadProperty(this object obj, string propName, Type viaType = null)
        {
            return (viaType ?? obj.GetType()).GetProperty(propName).GetValue(obj);
        }
    }

    static class Extensions
    {
        public static IOrderedEnumerable<T> Order<T>(this IEnumerable<T> e)
        {
            return e.OrderBy(x => x);
        }

        public static string JoinString<T>(this IEnumerable<T> values, string separator = null, string prefix = null, string suffix = null)
        {
            if (values == null)
                throw new ArgumentNullException("values");

            using (var enumerator = values.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    return "";

                // Optimise the case where there is only one element
                var one = enumerator.Current;
                if (!enumerator.MoveNext())
                    return prefix + one + suffix;

                // Optimise the case where there are only two elements
                var two = enumerator.Current;
                if (!enumerator.MoveNext())
                {
                    // Optimise the (common) case where there is no prefix/suffix; this prevents an array allocation when calling string.Concat()
                    if (prefix == null && suffix == null)
                        return one + separator + two;
                    return prefix + one + suffix + separator + prefix + two + suffix;
                }

                var sb = new StringBuilder()
                    .Append(prefix).Append(one).Append(suffix).Append(separator)
                    .Append(prefix).Append(two).Append(suffix);
                var prev = enumerator.Current;
                while (enumerator.MoveNext())
                {
                    sb.Append(separator).Append(prefix).Append(prev).Append(suffix);
                    prev = enumerator.Current;
                }
                sb.Append(separator).Append(prefix).Append(prev).Append(suffix);
                return sb.ToString();
            }
        }

        public static IEnumerable<T> SelectChain<T>(this T obj, Func<T, T> next) where T : class
        {
            while (obj != null)
            {
                yield return obj;
                obj = next(obj);
            }
        }
    }
}
