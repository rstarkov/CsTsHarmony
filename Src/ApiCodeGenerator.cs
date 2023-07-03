using System.Text;

namespace CsTsHarmony;

public class ApiCodeGenerator
{
    public ApiDesc Api { get; }
    public TsTypeConverterManager ConverterManager = new();

    public HashSet<string> Imports = new();
    public string ServicesClassName = "Services";
    public Func<string, string> ServiceClassName = n => n + "Service";
    public string ServiceClassExtends = "ApiServiceBase";
    public string ServiceOptionsType = "ApiServiceOptions";
    public string ImportFrom = null;
    public string ReturnTypeTemplate = "Promise<{0}>";
    public string Fetcher = "fetchJson";
    public Dictionary<Type, string> CustomFetchers = new() { [typeof(void)] = "fetchVoid", [typeof(string)] = "fetchString" };

    public ApiCodeGenerator(ApiDesc api)
    {
        Api = api;
    }

    public void Output(TypeScriptWriter writer)
    {
        if (ImportFrom != null)
            writer.Imports.Add($"import {{ {ServiceClassExtends}, {ServiceOptionsType} }} from '{ImportFrom}';");

        writer.WriteLine($"export class {ServicesClassName} {{");
        using (writer.Indent())
        {
            foreach (var svc in Api.Services.OrderBy(s => s.TsName))
                writer.WriteLine($"public readonly {svc.TsName}: {ServiceClassName(svc.TsName)};");
            writer.WriteLine();
            writer.WriteLine($"public constructor(options?: {ServiceOptionsType}) {{");
            using (writer.Indent())
            {
                foreach (var svc in Api.Services.OrderBy(s => s.TsName))
                    writer.WriteLine($"this.{svc.TsName} = new {ServiceClassName(svc.TsName)}(options);");
            }
            writer.WriteLine("}");
        }
        writer.WriteLine("}");
        writer.WriteLine();
        foreach (var svc in Api.Services.OrderBy(s => s.TsName))
        {
            OutputService(writer, svc);
            writer.WriteLine();
        }
        ConverterManager.OutputTypeConverters(writer);
    }

    protected void OutputService(TypeScriptWriter writer, ServiceDesc s)
    {
        writer.WriteLine($"export class {ServiceClassName(s.TsName)}{(ServiceClassExtends == null ? "" : $" extends {ServiceClassExtends}")} {{");
        writer.WriteLine();
        using (writer.Indent())
        {
            writer.WriteLine("public endpoints = {");
            using (writer.Indent())
            {
                foreach (var method in s.Methods.OrderBy(m => m.TsName))
                    foreach (var httpMethod in method.HttpMethods.Order())
                    {
                        var url = ApiGeneratorHelper.MethodUrlTemplateString(method, val => "${encodeURIComponent('' + " + val + ")}");
                        writer.WriteLine($"{getMethodName(method, httpMethod)}: ({getMethodParams(method.Parameters.Where(p => p.Location is ParameterLocation.UrlSegment or ParameterLocation.QueryString))}): string => `{url}`,");
                    }
            }
            writer.WriteLine("};");
            writer.WriteLine();
            writer.WriteLine($"public constructor(options?: {ServiceOptionsType}) {{");
            using (writer.Indent())
            {
                writer.WriteLine("super(options);");
                writer.WriteLine();

                foreach (var method in s.Methods.OrderBy(m => m.TsName))
                    foreach (var httpMethod in method.HttpMethods.Order())
                        writer.WriteLine($"this.{getMethodName(method, httpMethod)} = this.{getMethodName(method, httpMethod)}.bind(this);");
            }
            writer.WriteLine("}");
            writer.WriteLine();
            foreach (var method in s.Methods.OrderBy(m => m.TsName))
            {
                foreach (var httpMethod in method.HttpMethods.Order())
                {
                    bool canDirectReturn = !ConverterManager.NeedsConversion(method.ReturnType);
                    writer.Write($"public {(canDirectReturn ? "" : "async ")}{getMethodName(method, httpMethod)}(");
                    writer.Write(getMethodParams(method.Parameters));
                    writer.WriteLine($"): {string.Format(ReturnTypeTemplate, TypeScriptWriter.TypeSignature(method.ReturnType, ""))} {{");
                    using (writer.Indent())
                    {
                        writer.WriteLine($"let url = this.endpoints.{getMethodName(method, httpMethod)}({method.Parameters.Where(p => p.Location is ParameterLocation.UrlSegment or ParameterLocation.QueryString).Select(p => p.TsName).JoinString(", ")});");

                        // Output parameter type conversions
                        foreach (var p in method.Parameters)
                            ConverterManager.OutputTypeConversion(writer, p.TsName, p.Type, toTypeScript: false);

                        // Build request body
                        var bodyParams = method.Parameters.Where(p => p.Location == ParameterLocation.RequestBody).OrderBy(p => p.TsName).ToList();
                        if (bodyParams.Count > 1 && (method.BodyEncoding == BodyEncoding.Raw || method.BodyEncoding == BodyEncoding.Json))
                            throw new InvalidOperationException($"The body encoding for this method allows for at most one body parameter. Offending parameters: [{bodyParams.Select(p => p.TsName).JoinString(", ")}], method {method.Method.Name}, controller {s.ControllerType.FullName}");
                        var fetchOpts = $"method: '{httpMethod.ToUpper()}'";
                        if (bodyParams.Count > 0)
                        {
                            if (method.BodyEncoding == BodyEncoding.Raw)
                            {
                                fetchOpts += $", body: {bodyParams[0].TsName}";
                            }
                            else if (method.BodyEncoding == BodyEncoding.Json)
                            {
                                fetchOpts += $", body: JSON.stringify({bodyParams[0].TsName}), headers: {{ 'Content-Type': 'application/json' }}";
                            }
                            else if (method.BodyEncoding == BodyEncoding.FormUrlEncoded)
                            {
                                writer.WriteLine("let __body = new URLSearchParams();");
                                foreach (var bp in bodyParams)
                                    writer.WriteLine($"__body.append('{bp.TsName}', '' + {bp.TsName});");
                                fetchOpts += ", body: __body";
                            }
                            else if (method.BodyEncoding == BodyEncoding.MultipartFormData)
                            {
                                writer.WriteLine("let __body = new FormData();");
                                foreach (var bp in bodyParams)
                                    writer.WriteLine($"__body.append('{bp.TsName}', '' + {bp.TsName});");
                                fetchOpts += ", body: __body";
                                throw new NotImplementedException("FormData encoding is not fully implemented."); // no support for file name, parameter is always stringified with no support for Blob
                            }
                            else
                                throw new Exception($"Unexpected {nameof(method.BodyEncoding)}: {method.BodyEncoding}");
                        }

                        // Output call
                        var getter = Fetcher;
                        if (CustomFetchers.ContainsKey(method.ReturnType.RawType))
                            getter = CustomFetchers[method.ReturnType.RawType];
                        if (canDirectReturn)
                            writer.WriteLine($"return this.{getter}(url, {{ {fetchOpts} }}) as Promise<{TypeScriptWriter.TypeSignature(method.ReturnType, "")}>;");
                        else
                            writer.WriteLine($"let result = await this.{getter}(url, {{ {fetchOpts} }}) as {TypeScriptWriter.TypeSignature(method.ReturnType, "")};");

                        if (!canDirectReturn)
                        {
                            // Output return type conversion
                            ConverterManager.OutputTypeConversion(writer, "result", method.ReturnType, toTypeScript: true);
                            writer.WriteLine("return result;");
                        }
                    }
                    writer.WriteLine("}");
                    writer.WriteLine();
                }
            }
        }
        writer.WriteLine("}");
    }

    private string getMethodName(MethodDesc method, string httpMethod)
    {
        return method.TsName + (method.HttpMethods.Count == 1 ? "" : httpMethod.Substring(0, 1) + httpMethod.Substring(1).ToLower());
    }

    private string getMethodParams(IEnumerable<MethodParameterDesc> parameters)
    {
        var sb = new StringBuilder();
        bool first = true;
        foreach (var p in parameters)
        {
            if (!first)
                sb.Append(", ");
            sb.Append($"{p.TsName}{(p.Optional ? "?" : "")}: {TypeScriptWriter.TypeSignature(p.Type, "")}");
            first = false;
        }
        return sb.ToString();
    }
}

public class TsTypeGenerator
{
    private List<TypeDesc> _types;

    public TsTypeGenerator(IEnumerable<TypeDesc> types)
    {
        _types = types.ToList();
    }

    public void Output(TypeScriptWriter writer)
    {
        foreach (var t in _types.OfType<BasicTypeDesc>())
            if (t.TsConverter != null)
                foreach (var imp in t.TsConverter.GetImports())
                    writer.Imports.Add(imp);

        writer.WriteLine("declare global {");
        writer.WriteLine();
        using (writer.Indent())
        {
            var compositeTypes = _types.OfType<CompositeTypeDesc>();
            var enumTypes = _types.OfType<EnumTypeDesc>();
            var namespaces = compositeTypes.Select(t => t.TsNamespace).Concat(enumTypes.Select(t => t.TsNamespace)).Distinct().Order();
            foreach (var ns in namespaces)
            {
                if (ns != "")
                {
                    writer.WriteLine($"namespace {ns} {{");
                    writer.WriteLine();
                }
                using (writer.Indent(ns != ""))
                {
                    foreach (var t in enumTypes.Where(t => t.TsNamespace == ns).OrderBy(t => t.TsName))
                    {
                        OutputEnumTypeDeclaration(writer, t);
                        writer.WriteLine();
                    }
                    foreach (var t in compositeTypes.Where(t => t.TsNamespace == ns).OrderBy(t => t.TsName))
                    {
                        OutputCompositeTypeDeclaration(writer, t);
                        writer.WriteLine();
                    }
                }
                if (ns != "")
                {
                    writer.WriteLine("}");
                    writer.WriteLine();
                }
            }
        }
        writer.WriteLine("}");
        writer.WriteLine();
    }

    protected virtual void OutputEnumTypeDeclaration(TypeScriptWriter writer, EnumTypeDesc e)
    {
        writer.WriteLine($"type {e.TsName} = {e.Values.Select(v => v.Name).Order().JoinString(" | ", "\"", "\"")};");
    }

    protected virtual void OutputCompositeTypeDeclaration(TypeScriptWriter writer, CompositeTypeDesc ct)
    {
        writer.Write($"interface {ct.TsName}");
        if (ct.Extends.Any())
        {
            writer.Write(" extends ");
            writer.Write(ct.Extends.Select(t => TypeScriptWriter.TypeSignature(t, ct.TsNamespace)).Order().JoinString(", "));
        }
        writer.WriteLine(" {");
        using (writer.Indent())
        {
            foreach (var prop in ct.Properties.OrderBy(p => p.Name))
                writer.WriteLine($"{prop.Name}: {TypeScriptWriter.TypeSignature(prop.Type, ct.TsNamespace)};");
        }
        writer.WriteLine("}");
    }
}

public class TsTypeConverterManager
{
    private HashSet<TypeConverter> _convertersUsedFrom = new();
    private HashSet<TypeConverter> _convertersUsedTo = new();

    // If the key is present but the value is null, this means this type requires no conversion. The "Needed" properties on a converter keep track of whether anything uses it.
    private Dictionary<string, TypeConverter> _typeConverters = new();

    public void OutputTypeConverters(TypeScriptWriter writer)
    {
        foreach (var tc in _typeConverters.Values.Where(c => c != null).OrderBy(c => GetConverterName(c.ForType)))
        {
            if (_convertersUsedFrom.Contains(tc))
            {
                writer.WriteLine($"function {GetConverterFunctionName(tc.ForType, toTypeScript: false)}(val: {TypeScriptWriter.TypeSignature(tc.ForType, "")}): any {{");
                using (writer.Indent())
                    tc.WriteFunctionBody(writer, false);
                writer.WriteLine("}");
                writer.WriteLine();
            }
            if (_convertersUsedTo.Contains(tc))
            {
                writer.WriteLine($"function {GetConverterFunctionName(tc.ForType, toTypeScript: true)}(val: any): {TypeScriptWriter.TypeSignature(tc.ForType, "")} {{");
                using (writer.Indent())
                    tc.WriteFunctionBody(writer, true);
                writer.WriteLine("}");
                writer.WriteLine();
            }
        }
    }

    public bool NeedsConversion(TypeDesc type)
    {
        return getConverter(type) != null;
    }

    public void OutputTypeConversion(TypeScriptWriter writer, string lvalue, TypeDesc type, bool toTypeScript)
    {
        var converter = getConverter(type);
        if (converter == null)
            return;
        MarkUsedConverters(converter, toTypeScript);
        writer.WriteLine($"if ({lvalue})");
        using (writer.Indent())
            writer.WriteLine($"{lvalue} = {GetConverterFunctionName(converter.ForType, toTypeScript)}({lvalue});");
    }

    private void MarkUsedConverters(TypeConverter converter, bool toTypeScript)
    {
        var set = !toTypeScript ? _convertersUsedFrom : _convertersUsedTo;
        if (!set.Add(converter))
            return;
        foreach (var used in converter.UsesConverters)
            MarkUsedConverters(used, toTypeScript);
    }

    private class TypeConverter
    {
        public TypeDesc ForType;
        // conversion function is called only if the value to be converted is truthy
        public Action<TypeScriptWriter, bool> WriteFunctionBody;
        public HashSet<TypeConverter> UsesConverters = new HashSet<TypeConverter>();
    }

    private TypeConverter getConverter(TypeDesc type)
    {
        var key = GetConverterName(type);
        if (_typeConverters.TryGetValue(key, out var converter))
            return converter;

        converter = new TypeConverter { ForType = type };
        // there must be no early returns below; we must populate this converter and add it to the dictionary

        if (type is NullableTypeDesc nt)
            converter = getConverter(nt.ElementType);
        else if (type is ArrayTypeDesc at)
        {
            var elConverter = getConverter(at.ElementType);
            if (elConverter == null)
                converter = null;
            else
            {
                converter.UsesConverters.Add(elConverter);
                converter.WriteFunctionBody = (writer, toTypeScript) =>
                {
                    writer.WriteLine("for (let i = 0; i < val.length; i++)");
                    using (writer.Indent())
                    {
                        writer.WriteLine("if (val[i])");
                        using (writer.Indent())
                            writer.WriteLine($"val[i] = {GetConverterFunctionName(elConverter.ForType, toTypeScript)}(val[i]);");
                    }
                    writer.WriteLine("return val;");
                };
            }
        }
        else if (type is BasicTypeDesc bt)
        {
            if (bt.TsConverter == null)
                converter = null;
            else
            {
                converter.WriteFunctionBody = (writer, toTypeScript) =>
                {
                    if (!toTypeScript)
                        writer.WriteLine($"return {bt.TsConverter.ConvertFromTypeScript("val")};");
                    else
                        writer.WriteLine($"return {bt.TsConverter.ConvertToTypeScript("val")};");
                };
            }
        }
        else if (type is EnumTypeDesc et)
        {
            // TODO: Flags
            converter = null;
        }
        else if (type is CompositeTypeDesc ct)
        {
            var propConverters = ct.Properties.Select(p => new { prop = p, conv = getConverter(p.Type) }).Where(x => x.conv != null).ToList();
            if (propConverters.Count == 0)
                converter = null;
            else
            {
                foreach (var pc in propConverters)
                    converter.UsesConverters.Add(pc.conv);
                converter.WriteFunctionBody = (writer, toTypeScript) =>
                {
                    foreach (var pc in propConverters)
                    {
                        writer.WriteLine($"if (val.{pc.prop.Name})");
                        using (writer.Indent())
                            writer.WriteLine($"val.{pc.prop.Name} = {GetConverterFunctionName(pc.conv.ForType, toTypeScript)}(val.{pc.prop.Name});");
                    }
                    writer.WriteLine("return val;");
                };
            }
        }
        else
            throw new Exception();

        _typeConverters[key] = converter;
        return converter;
    }

    // TODO: there's no way to override this because the manager is not exposed
    protected virtual string DecorateConverterName(string name, bool toTypeScript)
    {
        return "convert" + (toTypeScript ? "ToTs_" : "FromTs_") + name;
    }

    protected virtual string GetConverterName(TypeDesc type)
    {
        if (type is NullableTypeDesc nt)
            return GetConverterName(nt.ElementType);
        else if (type is ArrayTypeDesc at)
            return GetConverterName(at.ElementType) + "Array";
        else
            return type.RawType.FullName.Replace(".", "");
    }

    private string GetConverterFunctionName(TypeDesc type, bool toTypeScript) => DecorateConverterName(GetConverterName(type), toTypeScript);
}

/// <summary>
/// Does not generate model types. This is only useful for integration tests in a test project that also references the server project to get the model types directly.
/// </summary>
public class CsTestClientGenerator
{
    private ApiDesc _api;

    public string ClassAccessibility = "internal";
    public string Namespace = null;
    public string ServicesClass = "ApiServices";

    public CsTestClientGenerator(ApiDesc api)
    {
        _api = api;
    }

    public void Output(string filename)
    {
        using var writer = new CodeWriter(filename);
        Output(writer);
    }

    private void Output(CodeWriter writer)
    {
        writer.WriteLine("// AUTOGENERATED FILE");
        writer.WriteLine("using System.Threading.Tasks;");
        writer.WriteLine();
        if (Namespace != null)
        {
            writer.WriteLine($"namespace {Namespace};");
            writer.WriteLine();
        }

        writer.WriteLine($"{ClassAccessibility} class {ServicesClass}");
        writer.WriteLine("{");
        using (writer.Indent())
        {
            foreach (var svc in _api.Services)
                writer.WriteLine($"public {svc.TsName}Service {svc.TsName};");
            writer.WriteLine();
            writer.WriteLine($"public {ServicesClass}(ApiServiceOptions options)");
            writer.WriteLine("{");
            using (writer.Indent())
                foreach (var svc in _api.Services)
                    writer.WriteLine($"{svc.TsName} = new(options);");
            writer.WriteLine("}");
        }
        writer.WriteLine("}");
        writer.WriteLine();

        foreach (var svc in _api.Services)
        {
            writer.WriteLine($"{ClassAccessibility} class {svc.TsName}Service : ApiServiceBase");
            writer.WriteLine("{");
            using (writer.Indent())
            {
                writer.WriteLine($"public static class Endpoints");
                writer.WriteLine("{");
                using (writer.Indent())
                {
                    foreach (var method in svc.Methods.OrderBy(m => m.TsName))
                        foreach (var httpMethod in method.HttpMethods.Order())
                            writer.WriteLine($"public static string {getMethodName(method, httpMethod)}({getMethodParams(method.Parameters.Where(p => p.Location is ParameterLocation.UrlSegment or ParameterLocation.QueryString))}) => $\"{ApiGeneratorHelper.MethodUrlTemplateString(method, val => "{UrlEncode(" + val + ")}")}\";");
                }
                writer.WriteLine("}");
                writer.WriteLine();
                writer.WriteLine($"public {svc.TsName}Service(ApiServiceOptions options) : base(options)");
                writer.WriteLine("{");
                writer.WriteLine("}");
                writer.WriteLine();
                foreach (var method in svc.Methods.OrderBy(m => m.TsName))
                    foreach (var httpMethod in method.HttpMethods.Order())
                    {
                        writer.Write($"public {getCsTaskType(method.ReturnType)} {getMethodName(method, httpMethod)}(");
                        writer.Write(getMethodParams(method.Parameters));
                        writer.WriteLine(")");
                        writer.WriteLine("{");
                        using (writer.Indent())
                        {
                            writer.WriteLine($"var url = Endpoints.{getMethodName(method, httpMethod)}({method.Parameters.Where(p => p.Location is ParameterLocation.UrlSegment or ParameterLocation.QueryString).Select(p => p.TsName).JoinString(", ")});");
                            //writer.WriteLine($"var url = $\"{Helper.MethodUrlTemplateString(method, val => "{UrlEncode(" + val + ")}")}\";");

                            var bodyParams = method.Parameters.Where(p => p.Location == ParameterLocation.RequestBody).OrderBy(p => p.TsName).ToList();
                            if (bodyParams.Count > 1 && (method.BodyEncoding == BodyEncoding.Raw || method.BodyEncoding == BodyEncoding.Json))
                                throw new InvalidOperationException($"The body encoding for this method allows for at most one body parameter. Offending parameters: [{bodyParams.Select(p => p.TsName).JoinString(", ")}], method {method.Method.Name}, controller {svc.ControllerType.FullName}");
                            var content = "null";
                            if (bodyParams.Count > 0)
                            {
                                if (method.BodyEncoding == BodyEncoding.Raw)
                                    content = $"RawContent({bodyParams[0].TsName})";
                                else if (method.BodyEncoding == BodyEncoding.Json)
                                    content = $"JsonContent({bodyParams[0].TsName})";
                                else if (method.BodyEncoding == BodyEncoding.FormUrlEncoded)
                                    throw new NotImplementedException();
                                else if (method.BodyEncoding == BodyEncoding.MultipartFormData)
                                    throw new NotImplementedException("FormData encoding is not fully implemented.");
                                else
                                    throw new Exception($"Unexpected {nameof(method.BodyEncoding)}: {method.BodyEncoding}");
                            }

                            var getter = $"FetchJson<{method.ReturnType}>";
                            if (method.ReturnType.RawType == typeof(string)) getter = "FetchString";
                            else if (method.ReturnType.RawType == typeof(void)) getter = "FetchVoid";
                            writer.WriteLine($"return {getter}(url, \"{httpMethod}\", {content});");
                        }
                        writer.WriteLine("}");
                    }
            }
            writer.WriteLine("}");
            writer.WriteLine();
        }
    }

    private string getMethodName(MethodDesc method, string httpMethod)
    {
        return method.TsName + (method.HttpMethods.Count == 1 ? "" : httpMethod.Substring(0, 1) + httpMethod.Substring(1).ToLower());
    }

    private string getMethodParams(IEnumerable<MethodParameterDesc> parameters)
    {
        var sb = new StringBuilder();
        bool first = true;
        foreach (var p in parameters)
        {
            if (!first)
                sb.Append(", ");
            sb.Append($"{getCs(p.Type)} {p.TsName}{(p.Optional ? " = default" : "")}");
            first = false;
        }
        return sb.ToString();
    }

    private string getCsTaskType(TypeDesc type)
    {
        if (type.RawType == typeof(void)) return "Task";
        return $"Task<{getCs(type)}>";
    }

    private string getCs(TypeDesc type)
    {
        if (type.RawType == typeof(void)) return "void";
        if (type.RawType == typeof(string)) return "string";
        if (type.RawType == typeof(int)) return "int";
        return type.ToString();
    }
}

internal static class ApiGeneratorHelper
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
        foreach (var p in method.Parameters.Where(p => p.Location == ParameterLocation.QueryString).OrderBy(p => p.TsName))
        {
            url.Append(first ? '?' : '&');
            url.Append(p.TsName + "=" + urlEncodeInString(p.TsName));
            first = false;
        }
        return url.ToString();
    }
}
