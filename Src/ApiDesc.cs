using System.Reflection;
using Microsoft.AspNetCore.Routing.Template;

namespace CsTsHarmony;

public class ServiceDesc
{
    public string TgtName;
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
    public string TgtName;
    public MethodInfo Method { get; }
    public ServiceDesc Service { get; }
    public TypeDesc ReturnType;
    public List<MethodParameterDesc> Parameters = new();
    public RouteTemplate UrlTemplate; // if we were going for full abstraction this would have to be wrapped into a *Desc class but we're not
    public string HttpMethod;
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
    /// <summary>Name of the parameter in the generated code. Can be changed arbitrarily.</summary>
    public string TgtName;
    /// <summary>Name of the parameter as expected by the server. Changing to the wrong value will break the calls.</summary>
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
    public Type SrcType { get; }

    public TypeDesc(Type srcType)
    {
        SrcType = srcType;
    }
}

public class BasicTypeDesc : TypeDesc
{
    public string TgtType;
    public override string ToString() => $"{TgtType} ({SrcType.Name})";
    public ITypeConverter TsConverter;

    public BasicTypeDesc(Type srcType, string tgtType) : base(srcType)
    {
        TgtType = tgtType;
    }
}

public class ArrayTypeDesc : TypeDesc
{
    public TypeDesc ElementType;

    public ArrayTypeDesc(Type srcType, TypeDesc elementType) : base(srcType)
    {
        ElementType = elementType;
    }

    public override string ToString() => $"{ElementType}[]";
}

public class NullableTypeDesc : TypeDesc
{
    public TypeDesc ElementType;

    public NullableTypeDesc(Type srcType, TypeDesc elementType) : base(srcType)
    {
        ElementType = elementType;
    }

    public override string ToString() => $"{ElementType} (nullable)";
}

public interface IDeclaredTypeDesc
{
    string TgtName { get; set; }
    string TgtNamespace { get; set; }
}

public class EnumTypeDesc : TypeDesc, IDeclaredTypeDesc
{
    public string TgtName { get; set; }
    public string TgtNamespace { get; set; }
    public List<EnumValueDesc> Values = new();
    public bool IsFlags;

    public override string ToString() => $"{SrcType.FullName} (enum)";

    public EnumTypeDesc(Type srcType) : base(srcType)
    {
        TgtName = srcType.Name;
        TgtNamespace = srcType.Namespace;
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
    public string TgtName { get; set; }
    public string TgtNamespace { get; set; }
    public List<PropertyDesc> Properties = new();
    public List<CompositeTypeDesc> Extends = new();

    public override string ToString() => $"{SrcType.FullName} (composite)";

    public CompositeTypeDesc(Type srcType) : base(srcType)
    {
        TgtName = srcType.Name;
        TgtNamespace = srcType.Namespace;
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
