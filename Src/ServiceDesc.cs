using System.Reflection;
using Microsoft.AspNetCore.Routing.Template;

namespace CsTsHarmony;

public class ServiceDesc
{
    public string TgtName;
    public Func<string, string> TgtTypeName = tgtName => $"{tgtName}Service";
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
    public string Fetcher;

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
    public ParameterInfo Parameter { get; }
    public MethodDesc Method { get; }
    public TypeDesc Type;
    public ParameterLocation Location;
    public bool Optional;

    public MethodParameterDesc(ParameterInfo parameter, MethodDesc method)
    {
        Parameter = parameter;
        Method = method;
    }
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
