using System.Reflection;

namespace CsTsHarmony;

public class IgnoreConfig<T> where T : MemberInfo
{
    public HashSet<T> Ignored = new();
    public Func<T, bool> Filter = null;
    public HashSet<string> Attributes = new() { "Newtonsoft.Json.JsonIgnoreAttribute" };

    public bool Include(T value)
    {
        if (Ignored.Contains(value))
            return false;
        if (value.CustomAttributes.Any(ca => Attributes.Contains(ca.AttributeType.FullName)))
        {
            Ignored.Add(value);
            return false;
        }
        if (Filter != null && !Filter(value))
        {
            Ignored.Add(value);
            return false;
        }
        return true;
    }
}
