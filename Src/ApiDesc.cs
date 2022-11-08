using System.Reflection;
using Microsoft.AspNetCore.Routing.Template;

namespace CsTsHarmony;

public class ApiDesc
{
    public List<ServiceDesc> Services = new();
    public Dictionary<Type, TypeDesc> Types = new();
}

public class ServiceDesc
{
    public string TsName;
    public Type ControllerType { get; }
    public List<MethodDesc> Methods = new();

    public override string ToString() => $"{ControllerType.Name} ({ControllerType.FullName})";

    public ServiceDesc(Type controllerType)
    {
        ControllerType = controllerType;
    }
}

public class MethodDesc
{
    public string TsName;
    public MethodInfo Method { get; }
    public ServiceDesc Service { get; }
    public TypeRef ReturnType;
    public List<MethodParameterDesc> Parameters = new();
    public RouteTemplate UrlTemplate; // if we were going for full abstraction this would have to be wrapped into a *Desc class but we're not
    public List<string> HttpMethods = new List<string>();
    public BodyEncoding BodyEncoding;

    public override string ToString() => $"{Method.Name} on {Service}";

    public MethodDesc(MethodInfo method, ServiceDesc service)
    {
        Method = method;
        Service = service;
    }
}

public class MethodParameterDesc
{
    public string TsName;
    public string RequestName;
    public MethodDesc Method { get; }
    public TypeRef Type;
    public ParameterLocation Location;
    public bool Optional;

    public MethodParameterDesc(MethodDesc method)
    {
        Method = method;
    }
}

public class TypeRef
{
    public Type RawType;
    public bool Nullable;
    public bool Array;
    public bool ArrayNullable;
    public TypeDesc MappedType;

    public override string ToString() => $"{RawType}{(Nullable ? "?" : "")}{(Array ? "[]" : "")}{(Array && ArrayNullable ? "?" : "")}";
}

public abstract class TypeDesc
{
    public string TsName; // null if this type does not require a declaration to be emitted
    public string TsNamespace;
    public Type RawType { get; }

    public TypeDesc(Type rawType)
    {
        RawType = rawType;
    }

    public virtual string TsReference(string fromNamespace) => fromNamespace == TsNamespace ? TsName : $"{TsNamespace}.{TsName}";
}

public class BasicTypeDesc : TypeDesc
{
    public string TsType;
    public override string ToString() => $"{TsType} ({RawType.Name})";

    public BasicTypeDesc(Type rawType, string tsType) : base(rawType)
    {
        TsType = tsType;
        TsName = null;
        TsNamespace = null;
    }

    public override string TsReference(string fromNamespace) => TsType;
}

public class EnumTypeDesc : TypeDesc
{
    public List<EnumValueDesc> Values = new();
    public bool IsFlags;

    public override string ToString() => $"{RawType.FullName} (enum)";

    public EnumTypeDesc(Type rawType) : base(rawType)
    {
        TsName = rawType.Name;
        TsNamespace = rawType.Namespace;
    }
}

public class EnumValueDesc
{
    public string Name;
    public long Value;

    public EnumTypeDesc Type { get; }

    public override string ToString() => $"{Name} = {Value}";

    public EnumValueDesc(EnumTypeDesc type)
    {
        Type = type;
    }
}

public class CompositeTypeDesc : TypeDesc
{
    public List<PropertyDesc> Properties = new();
    public List<TypeRef> Extends = new();

    public override string ToString() => $"{RawType.FullName} (composite)";

    public CompositeTypeDesc(Type rawType) : base(rawType)
    {
        TsName = rawType.Name;
        TsNamespace = rawType.Namespace;
    }
}

public class PropertyDesc
{
    public string Name;
    public TypeRef Type;

    public override string ToString() => $"{Name}: {Type}";
}

public enum BodyEncoding
{
    /// <summary>
    ///     At most one body parameter is allowed, and is expected to be of type string, Blob, File or ArrayBuffer /
    ///     ArrayBufferView.</summary>
    Raw = 1,
    /// <summary>At most one body parameter is allowed, and is encoded using JSON.stringify.</summary>
    Json,
    MultipartFormData,
    FormUrlEncoded,
}

public enum ParameterLocation
{
    UrlSegment = 1,
    QueryString,
    RequestBody,
}
