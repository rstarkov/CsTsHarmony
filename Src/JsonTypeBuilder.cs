using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace CsTsHarmony;

public class JsonTypeBuilder
{
    public Dictionary<Type, BasicTypeDesc> BasicTypeMap { get; set; } = new();

    public IgnoreConfig<PropertyInfo> IgnoreProperties = new();
    public BindingFlags PropertyBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    public IgnoreConfig<FieldInfo> IgnoreFields = new();
    public BindingFlags FieldBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    public HashSet<Type> DescendantCandidates = new();

    private Dictionary<Type, TypeDesc> _types = new(); // also contains null values for types that can't be mapped
    public IEnumerable<TypeDesc> Types => _types.Values.Where(t => t != null);

    public JsonTypeBuilder()
    {
        ConfigureBasicType(typeof(void), "void");
        ConfigureBasicType(typeof(object), "any");
        ConfigureBasicType(typeof(ValueType), "any");

        //ConfigureBasicType("Newtonsoft.Json.Linq.JObject", "any");
        //ConfigureBasicType("System.Text.Json.Nodes.JsonObject", "any");

        ConfigureBasicType(typeof(string), "string");
        ConfigureBasicType(typeof(char), "string");
        ConfigureBasicType(typeof(Guid), "string");

        ConfigureBasicType(typeof(DateTime), "string");
        ConfigureBasicType(typeof(DateTimeOffset), "string");
        ConfigureBasicType(typeof(DateOnly), "string");
        ConfigureBasicType(typeof(TimeSpan), "string");
        ConfigureBasicType(typeof(Uri), "string");
        ConfigureBasicType(typeof(Version), "string");

        ConfigureBasicType(typeof(byte), "number");
        ConfigureBasicType(typeof(sbyte), "number");
        ConfigureBasicType(typeof(short), "number");
        ConfigureBasicType(typeof(ushort), "number");
        ConfigureBasicType(typeof(int), "number");
        ConfigureBasicType(typeof(uint), "number");
        ConfigureBasicType(typeof(long), "number");
        ConfigureBasicType(typeof(ulong), "number");
        ConfigureBasicType(typeof(double), "number");
        ConfigureBasicType(typeof(decimal), "number");

        ConfigureBasicType(typeof(bool), "boolean");
    }

    public void ConfigureBasicType(Type type, string tgtType)
    {
        BasicTypeMap[type] = new BasicTypeDesc(type, tgtType);
    }

    public void ConfigureBasicType(Type type, string tgtType, Func<string, string> toTs, Func<string, string> fromTs, params string[] imports)
    {
        BasicTypeMap[type] = new BasicTypeDesc(type, tgtType) { TsConverter = new LambdaTypeConverter { FromTypeScript = fromTs, ToTypeScript = toTs, Imports = imports } };
    }

    public TypeDesc AddType(Type type)
    {
        type = UnwrapType(type);
        if (!_types.ContainsKey(type))
            _types[type] = MapBasicType(type) ?? MapArrayType(type) ?? MapNullableType(type) ?? MapEnumType(type) ?? MapCompositeType(type); // or null
        return _types[type];
    }

    // it's not recursive; we don't expect or support nesting - but Task<ActionResult> is supported
    protected virtual Type UnwrapType(Type type)
    {
        if (type == typeof(Task))
            type = typeof(void);
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            type = type.GetGenericArguments()[0];

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ActionResult<>))
            type = type.GetGenericArguments()[0];
        else if (type == typeof(ActionResult) || type.IsAssignableTo(typeof(IActionResult)) || type.IsAssignableTo(typeof(IConvertToActionResult)))
            type = typeof(object);

        return type;
    }

    protected virtual TypeDesc MapBasicType(Type type)
    {
        if (BasicTypeMap.TryGetValue(type, out var result))
            return result;
        return null;
    }

    protected virtual TypeDesc MapArrayType(Type type)
    {
        if (type == typeof(string))
            return null;

        if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var t = AddType(type.GetGenericArguments()[0]);
            if (t == null)
                return null;
            return new ArrayTypeDesc(type, t);
        }

        var ts = type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)).ToList();
        if (ts.Count == 1)
        {
            var t = AddType(ts.FirstOrDefault()?.GetGenericArguments()[0]);
            if (t == null)
                return null;
            return new ArrayTypeDesc(type, t);
        }

        return null;
    }

    protected virtual TypeDesc MapNullableType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var t = AddType(type.GetGenericArguments()[0]);
            if (t == null)
                return null;
            return new NullableTypeDesc(type, t);
        }
        return null;
    }

    protected virtual TypeDesc MapEnumType(Type type)
    {
        if (!type.IsEnum)
            return null;
        var td = new EnumTypeDesc(type);
        td.IsFlags = type.GetCustomAttribute<FlagsAttribute>() != null;
        td.Values = type.GetEnumValues().Cast<object>().Select(v => new EnumValueDesc(td) { Value = Convert.ToInt64(v), Name = v.ToString() }).ToList();
        return td;
    }

    protected virtual TypeDesc MapCompositeType(Type type)
    {
        // Look for descendants of this type - including types that implement an interface
        if (DescendantCandidates.Contains(type))
        {
            var descendants = DescendantCandidates.Where(c => c != type && c != typeof(object) && c.IsAssignableTo(type)).ToList();
            foreach (var d in descendants)
                AddType(d);
        }

        var ct = new CompositeTypeDesc(type);
        foreach (var prop in type.GetProperties(PropertyBindingFlags))
        {
            if (!IgnoreProperties.Include(prop))
                continue;
            var proptype = AddType(prop.PropertyType);
            if (proptype != null)
                ct.Properties.Add(new PropertyDesc { Name = prop.Name, Type = proptype });
            else
                IgnoreProperties.Ignored.Add(prop);
        }
        foreach (var field in type.GetFields(FieldBindingFlags))
        {
            if (!IgnoreFields.Include(field))
                continue;
            var fieldtype = AddType(field.FieldType);
            if (fieldtype != null)
                ct.Properties.Add(new PropertyDesc { Name = field.Name, Type = fieldtype });
            else
                IgnoreFields.Ignored.Add(field);
        }

        // Populate Extends: the base type
        if (type.BaseType != null && DescendantCandidates.Contains(type.BaseType))
            ct.Extends.Add((CompositeTypeDesc)AddType(type.BaseType));
        // Populate Extends: interfaces
        var ifaces = type.GetInterfaces().Where(i => DescendantCandidates.Contains(i)).ToHashSet();
        foreach (var i1 in ifaces.ToList()) // exclude interfaces that are inherited via other interfaces
            if (ifaces.Any(i2 => i2 != i1 && i2.IsAssignableTo(i1)))
                ifaces.Remove(i1);
        foreach (var i in ifaces)
            ct.Extends.Add((CompositeTypeDesc)AddType(i));
        // Remove unmappable types
        ct.Extends.RemoveAll(t => t == null);
        return ct;
    }
}
