using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace CsTsHarmony;

public static class HarmonyUtil
{
    public static string MethodUrlTemplateString(MethodDesc method, Func<string, string> urlEncodeInString)
    {
        var url = new StringBuilder();
        bool first = true;
        foreach (var segment in method.UrlTemplate.Segments)
        {
            if (first)
                first = false;
            else
                url.Append('/');
            if (!segment.IsSimple)
                throw new NotImplementedException(); // need a test case to implement this
            if (segment.Parts[0].IsLiteral)
                url.Append(segment.Parts[0].Text);
            else if (segment.Parts[0].IsParameter)
                url.Append(urlEncodeInString(segment.Parts[0].Name));
            else
                throw new NotImplementedException(); // need a test case to implement this
        }
        first = true;
        foreach (var p in method.Parameters.Where(p => p.Location == ParameterLocation.QueryString).OrderBy(p => p.RequestName))
        {
            url.Append(first ? '?' : '&');
            url.Append(p.RequestName + "=" + urlEncodeInString(p.TgtName));
            first = false;
        }
        return url.ToString();
    }

    public static Type UnwrapType(Type type, bool preserveActionResults = false)
    {
        if (type == typeof(Task))
            type = typeof(void);
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            type = type.GetGenericArguments()[0];

        if (preserveActionResults)
            return type;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ActionResult<>))
            type = type.GetGenericArguments()[0];
        else if (type == typeof(ActionResult) || type.IsAssignableTo(typeof(IActionResult)) || type.IsAssignableTo(typeof(IConvertToActionResult)))
            type = typeof(object);

        return type;
    }
}

internal static class ExtensionMethods
{
    public static IEnumerable<T> Order<T>(this IEnumerable<T> source)
    {
        return source.OrderBy(k => k);
    }

    public static string JoinString<T>(this IEnumerable<T> values, string separator = null, string prefix = null, string suffix = null, string lastSeparator = null)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));
        if (lastSeparator == null)
            lastSeparator = separator;

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
                    return one + lastSeparator + two;
                return prefix + one + suffix + lastSeparator + prefix + two + suffix;
            }

            StringBuilder sb = new StringBuilder()
                .Append(prefix).Append(one).Append(suffix).Append(separator)
                .Append(prefix).Append(two).Append(suffix);
            var prev = enumerator.Current;
            while (enumerator.MoveNext())
            {
                sb.Append(separator).Append(prefix).Append(prev).Append(suffix);
                prev = enumerator.Current;
            }
            sb.Append(lastSeparator).Append(prefix).Append(prev).Append(suffix);
            return sb.ToString();
        }
    }
}
