using System.Reflection;
using Microsoft.AspNetCore.Routing.Template;

namespace CsTsHarmony;

public class ApiDesc
{
    public List<ServiceDesc> Services = new();
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
    public TypeDesc ReturnType;
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
    public TypeDesc Type;
    public ParameterLocation Location;
    public bool Optional;

    public MethodParameterDesc(MethodDesc method)
    {
        Method = method;
    }
}

public abstract class TypeDesc
{
    public Type RawType { get; }

    public TypeDesc(Type rawType)
    {
        RawType = rawType;
    }
}

public class BasicTypeDesc : TypeDesc
{
    public string TsType;
    public override string ToString() => $"{TsType} ({RawType.Name})";
    public ITypeConverter TsConverter;

    public BasicTypeDesc(Type rawType, string tsType) : base(rawType)
    {
        TsType = tsType;
    }
}

public class ArrayTypeDesc : TypeDesc
{
    public TypeDesc ElementType;

    public ArrayTypeDesc(Type rawType, TypeDesc elementType) : base(rawType)
    {
        ElementType = elementType;
    }

    public override string ToString() => $"{ElementType}[]";
}

public class NullableTypeDesc : TypeDesc
{
    public TypeDesc ElementType;

    public NullableTypeDesc(Type rawType, TypeDesc elementType) : base(rawType)
    {
        ElementType = elementType;
    }

    public override string ToString() => $"{ElementType} (nullable)";
}

public interface IDeclaredTypeDesc
{
    string TsName { get; set; }
    string TsNamespace { get; set; }
}

public class EnumTypeDesc : TypeDesc, IDeclaredTypeDesc
{
    public string TsName { get; set; }
    public string TsNamespace { get; set; }
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

public class CompositeTypeDesc : TypeDesc, IDeclaredTypeDesc
{
    public string TsName { get; set; }
    public string TsNamespace { get; set; }
    public List<PropertyDesc> Properties = new();
    public List<CompositeTypeDesc> Extends = new();

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
    public TypeDesc Type;

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
