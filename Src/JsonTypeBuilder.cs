using System.Reflection;

namespace CsTsHarmony;

public interface ITypeBuilder
{
    TypeDesc AddType(Type type);
}

public class JsonTypeBuilder : ITypeBuilder
{
    public Dictionary<Type, BasicTypeDesc> BasicTypeMap { get; set; } = new();

    public IgnoreConfig<PropertyInfo> IgnoreProperties = new();
    public BindingFlags PropertyBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    public IgnoreConfig<FieldInfo> IgnoreFields = new();
    public BindingFlags FieldBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    public HashSet<Type> DescendantCandidates = new();

    protected Dictionary<Type, TypeDesc> _types = new(); // also contains null values for types that can't be mapped
    public IEnumerable<TypeDesc> Types => _types.Values.Where(t => t != null);

    public void ConfigureForTs()
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
        type = HarmonyUtil.UnwrapType(type);
        if (!_types.ContainsKey(type))
            MapType(type);
        return _types[type];
    }

    /// <summary>
    ///     Maps the type and stores the result in `_types[type]`. Stores null if the type cannot be mapped. Called only for
    ///     types that don't exist in `_types`. Must store the final TypeDesc instance before making any calls to AddType.</summary>
    protected virtual void MapType(Type type)
    {
        if (MapBasicType(type)) return;
        if (MapArrayType(type)) return;
        if (MapNullableType(type)) return;
        if (MapEnumType(type)) return;
        if (MapCompositeType(type)) return;
        _types[type] = null;
    }

    protected virtual bool MapBasicType(Type type)
    {
        if (BasicTypeMap.TryGetValue(type, out var result))
        {
            _types[type] = result;
            return true;
        }
        return false;
    }

    protected Dictionary<Type, TypeDesc> AddTypeProvisionally(TypeDesc t)
    {
        var backup = new Dictionary<Type, TypeDesc>(_types);
        _types[t.SrcType] = t;
        return backup;
    }
    protected void AddTypeRollback(TypeDesc t, Dictionary<Type, TypeDesc> backup)
    {
        _types = backup;
        _types[t.SrcType] = null;
    }

    protected virtual bool MapArrayType(Type type)
    {
        if (type == typeof(string))
            return false;

        if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var at = new ArrayTypeDesc(type, null);
            var backup = AddTypeProvisionally(at);
            at.ElementType = AddType(type.GetGenericArguments()[0]);
            if (at.ElementType == null)
                AddTypeRollback(at, backup);
            return true;
        }

        var ts = type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)).ToList();
        if (ts.Count == 1)
        {
            var at = new ArrayTypeDesc(type, null);
            var backup = AddTypeProvisionally(at);
            at.ElementType = AddType(ts.FirstOrDefault()?.GetGenericArguments()[0]);
            if (at.ElementType == null)
                AddTypeRollback(at, backup);
            return true;
        }

        return false;
    }

    protected virtual bool MapNullableType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var nt = new NullableTypeDesc(type, null);
            var backup = AddTypeProvisionally(nt);
            nt.ElementType = AddType(type.GetGenericArguments()[0]);
            if (nt.ElementType == null)
                AddTypeRollback(nt, backup);
            return true;
        }
        return false;
    }

    protected virtual bool MapEnumType(Type type)
    {
        if (!type.IsEnum)
            return false;
        var td = new EnumTypeDesc(type);
        td.IsFlags = type.GetCustomAttribute<FlagsAttribute>() != null;
        td.Values = type.GetEnumValues().Cast<object>().Select(v => new EnumValueDesc(td) { Value = Convert.ToInt64(v), Name = v.ToString() }).ToList();
        _types[type] = td;
        return true;
    }

    protected virtual bool MapCompositeType(Type type)
    {
        var ct = new CompositeTypeDesc(type);
        _types[type] = ct; // this method doesn't need to roll back because it will always complete: any failures to map are removed from the composite type member list instead

        // Look for descendants of this type - including types that implement an interface
        if (DescendantCandidates.Contains(type))
        {
            var descendants = DescendantCandidates.Where(c => c != type && c != typeof(object) && c.IsAssignableTo(type)).ToList();
            foreach (var d in descendants)
                AddType(d);
        }

        PropertyDesc makePropertyDesc(string name, TypeDesc proptype, NullabilityInfo nullability)
        {
            var desc = new PropertyDesc { Name = name, Type = proptype };
            desc.Nullable = nullability.WriteState == NullabilityState.Nullable ? true : nullability.WriteState == NullabilityState.NotNull ? false : null;
            if (desc.Nullable == true && proptype is NullableTypeDesc nt)
                desc.Type = nt.ElementType;
            return desc;
        }
        foreach (var prop in type.GetProperties(PropertyBindingFlags))
        {
            if (!IgnoreProperties.Include(prop))
                continue;
            var proptype = AddType(prop.PropertyType);
            if (proptype != null)
                ct.Properties.Add(makePropertyDesc(prop.Name, proptype, new NullabilityInfoContext().Create(prop)));
            else
                IgnoreProperties.Ignored.Add(prop);
        }
        foreach (var field in type.GetFields(FieldBindingFlags))
        {
            if (!IgnoreFields.Include(field))
                continue;
            var fieldtype = AddType(field.FieldType);
            if (fieldtype != null)
                ct.Properties.Add(makePropertyDesc(field.Name, fieldtype, new NullabilityInfoContext().Create(field)));
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

        return true;
    }
}
