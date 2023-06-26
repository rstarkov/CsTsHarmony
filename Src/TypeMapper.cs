using System.Reflection;

namespace CsTsHarmony;

public interface ITypeMapper
{
    TypeDesc MapType(Type type, Func<Type, TypeRef> referenceType);
}

public class LambdaTypeMapper : ITypeMapper
{
    public Dictionary<Type, TypeDesc> TypeMap { get; set; } = new();

    public TypeDesc MapType(Type type, Func<Type, TypeRef> referenceType)
    {
        return TypeMap.ContainsKey(type) ? TypeMap[type] : null;
    }

    public void AddBasic(Type csType, string tsType, Func<string, string> toTs, Func<string, string> fromTs, params string[] imports)
    {
        TypeMap.Add(csType, new BasicTypeDesc(csType, tsType) { TsConverter = new LambdaTypeConverter { FromTypeScript = fromTs, ToTypeScript = toTs, Imports = imports } });
    }
}

public class BasicTypeMapper : ITypeMapper
{
    public Dictionary<string, string> TypeMap { get; set; }

    public BasicTypeMapper()
    {
        TypeMap = new Dictionary<string, string>
        {
            [typeof(void).FullName] = "void",
            [typeof(object).FullName] = "any",
            [typeof(ValueType).FullName] = "any",

            ["Newtonsoft.Json.Linq.JObject"] = "any",
            ["System.Text.Json.Nodes.JsonObject"] = "any",

            [typeof(string).FullName] = "string",
            [typeof(char).FullName] = "string",
            [typeof(Guid).FullName] = "string",

            [typeof(DateTime).FullName] = "string",
            [typeof(DateTimeOffset).FullName] = "string",
            [typeof(DateOnly).FullName] = "string",
            [typeof(TimeSpan).FullName] = "string",
            [typeof(Uri).FullName] = "string",
            [typeof(Version).FullName] = "string",

            [typeof(byte).FullName] = "number",
            [typeof(sbyte).FullName] = "number",
            [typeof(short).FullName] = "number",
            [typeof(ushort).FullName] = "number",
            [typeof(int).FullName] = "number",
            [typeof(uint).FullName] = "number",
            [typeof(long).FullName] = "number",
            [typeof(ulong).FullName] = "number",
            [typeof(double).FullName] = "number",
            [typeof(decimal).FullName] = "number",

            [typeof(bool).FullName] = "boolean",
        };
    }

    public TypeDesc MapType(Type type, Func<Type, TypeRef> referenceType)
    {
        return TypeMap.ContainsKey(type.FullName) ? new BasicTypeDesc(type, TypeMap[type.FullName]) : null;
    }
}

public class EnumTypeMapper : ITypeMapper
{
    public TypeDesc MapType(Type type, Func<Type, TypeRef> referenceType)
    {
        if (!type.IsEnum)
            return null;
        var td = new EnumTypeDesc(type);
        td.IsFlags = type.GetCustomAttribute<FlagsAttribute>() != null;
        td.Values = type.GetEnumValues().Cast<object>().Select(v => new EnumValueDesc(td) { Value = Convert.ToInt64(v), Name = v.ToString() }).ToList();
        return td;
    }
}

public class CompositeTypeMapper : ITypeMapper
{
    public IgnoreConfig<PropertyInfo> IgnoreProperties = new();
    public BindingFlags PropertyBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    public IgnoreConfig<FieldInfo> IgnoreFields = new();
    public BindingFlags FieldBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    public HashSet<Type> DescendantCandidates = new();

    public TypeDesc MapType(Type type, Func<Type, TypeRef> referenceType)
    {
        // Look for descendants of this type - including types that implement an interface
        if (DescendantCandidates.Contains(type))
        {
            var descendants = DescendantCandidates.Where(c => c != type && c != typeof(object) && c.IsAssignableTo(type)).ToList();
            foreach (var d in descendants)
                referenceType(d);
        }

        var ct = new CompositeTypeDesc(type);
        foreach (var prop in type.GetProperties(PropertyBindingFlags))
        {
            if (!IgnoreProperties.Include(prop))
                continue;
            var proptype = referenceType(prop.PropertyType);
            if (proptype != null)
                ct.Properties.Add(new PropertyDesc { Name = prop.Name, Type = proptype });
            else
                IgnoreProperties.Ignored.Add(prop);
        }
        foreach (var field in type.GetFields(FieldBindingFlags))
        {
            if (!IgnoreFields.Include(field))
                continue;
            var fieldtype = referenceType(field.FieldType);
            if (fieldtype != null)
                ct.Properties.Add(new PropertyDesc { Name = field.Name, Type = fieldtype });
            else
                IgnoreFields.Ignored.Add(field);
        }

        // Populate Extends: the base type
        if (type.BaseType != null && DescendantCandidates.Contains(type.BaseType))
            ct.Extends.Add(referenceType(type.BaseType));
        // Populate Extends: interfaces
        var ifaces = type.GetInterfaces().Where(i => DescendantCandidates.Contains(i)).ToHashSet();
        foreach (var i1 in ifaces.ToList()) // exclude interfaces that are inherited via other interfaces
            if (ifaces.Any(i2 => i2 != i1 && i2.IsAssignableTo(i1)))
                ifaces.Remove(i1);
        foreach (var i in ifaces)
            ct.Extends.Add(referenceType(i));
        return ct;
    }
}
