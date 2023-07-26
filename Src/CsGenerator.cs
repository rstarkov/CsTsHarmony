﻿using System.Text;

namespace CsTsHarmony;

/// <summary>
///     A simplified generator targeting C# which assumes all the model types are already in scope and no type conversions are
///     necessary. Intended primarily for integration tests.</summary>
public class CsTestClientGenerator
{
    public string ClassAccessibility = "internal";
    public string Namespace = null;
    public string ServicesClass = "ApiServices";

    public void Output(string filename, IEnumerable<ServiceDesc> services)
    {
        using var writer = new CodeWriter(filename);
        Output(writer, services);
    }

    private void Output(CodeWriter writer, IEnumerable<ServiceDesc> services)
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
            foreach (var svc in services)
                writer.WriteLine($"public {svc.TgtName}Service {svc.TgtName};");
            writer.WriteLine();
            writer.WriteLine($"public {ServicesClass}(ApiServiceOptions options)");
            writer.WriteLine("{");
            using (writer.Indent())
                foreach (var svc in services)
                    writer.WriteLine($"{svc.TgtName} = new(options);");
            writer.WriteLine("}");
        }
        writer.WriteLine("}");
        writer.WriteLine();

        foreach (var svc in services)
        {
            writer.WriteLine($"{ClassAccessibility} class {svc.TgtName}Service : ApiServiceBase");
            writer.WriteLine("{");
            using (writer.Indent())
            {
                writer.WriteLine($"public static class Endpoints");
                writer.WriteLine("{");
                using (writer.Indent())
                {
                    foreach (var method in svc.Methods.OrderBy(m => m.TgtName))
                        writer.WriteLine($"public static string {method.TgtName}({getMethodParams(method.Parameters.Where(p => p.Location is ParameterLocation.UrlSegment or ParameterLocation.QueryString))}) => $\"{HarmonyUtil.MethodUrlTemplateString(method, val => "{UrlEncode(" + val + ")}")}\";");
                }
                writer.WriteLine("}");
                writer.WriteLine();
                writer.WriteLine($"public {svc.TgtName}Service(ApiServiceOptions options) : base(options)");
                writer.WriteLine("{");
                writer.WriteLine("}");
                writer.WriteLine();
                foreach (var method in svc.Methods.OrderBy(m => m.TgtName))
                {
                    var fetcher = method.Fetcher[..1].ToUpper() + method.Fetcher[1..];
                    if (fetcher == "FetchJson")
                        fetcher = $"FetchJson<{getCs(method.ReturnType)}>";
                    var returnType = fetcher == "FetchVoid" ? "Task" : $"Task<{getCs(method.ReturnType)}>";

                    writer.Write($"public {returnType} {method.TgtName}(");
                    writer.Write(getMethodParams(method.Parameters));
                    writer.WriteLine(")");
                    writer.WriteLine("{");
                    using (writer.Indent())
                    {
                        writer.WriteLine($"var url = Endpoints.{method.TgtName}({method.Parameters.Where(p => p.Location is ParameterLocation.UrlSegment or ParameterLocation.QueryString).Select(p => p.TgtName).JoinString(", ")});");
                        //writer.WriteLine($"var url = $\"{Helper.MethodUrlTemplateString(method, val => "{UrlEncode(" + val + ")}")}\";");

                        var bodyParams = method.Parameters.Where(p => p.Location == ParameterLocation.RequestBody).OrderBy(p => p.TgtName).ToList();
                        if (bodyParams.Count > 1 && (method.BodyEncoding == BodyEncoding.Raw || method.BodyEncoding == BodyEncoding.Json))
                            throw new InvalidOperationException($"The body encoding for this method allows for at most one body parameter. Offending parameters: [{bodyParams.Select(p => p.TgtName).JoinString(", ")}], method {method.Method.Name}, controller {svc.ControllerType.FullName}");
                        var content = "null";
                        if (bodyParams.Count > 0)
                        {
                            if (method.BodyEncoding == BodyEncoding.Raw)
                                content = $"RawContent({bodyParams[0].TgtName})";
                            else if (method.BodyEncoding == BodyEncoding.Json)
                                content = $"JsonContent({bodyParams[0].TgtName})";
                            else if (method.BodyEncoding == BodyEncoding.FormUrlEncoded)
                                throw new NotImplementedException();
                            else if (method.BodyEncoding == BodyEncoding.MultipartFormData)
                                throw new NotImplementedException("FormData encoding is not fully implemented.");
                            else
                                throw new Exception($"Unexpected {nameof(method.BodyEncoding)}: {method.BodyEncoding}");
                        }

                        writer.WriteLine($"return {fetcher}(url, \"{method.HttpMethod}\", {content});");
                    }
                    writer.WriteLine("}");
                }
            }
            writer.WriteLine("}");
            writer.WriteLine();
        }
    }

    private string getMethodParams(IEnumerable<MethodParameterDesc> parameters)
    {
        var sb = new StringBuilder();
        bool first = true;
        foreach (var p in parameters)
        {
            if (!first)
                sb.Append(", ");
            sb.Append($"{getCs(p.Type)} {p.TgtName}{(p.Optional ? " = default" : "")}");
            first = false;
        }
        return sb.ToString();
    }

    private string getCs(TypeDesc type)
    {
        if (type is BasicTypeDesc bt)
            return bt.TgtType;
        else if (type is NullableTypeDesc nt)
            return getCs(nt.ElementType) + "?";
        else if (type is ArrayTypeDesc at)
            return getCs(at.ElementType) + "[]";
        else
            throw new Exception($"This simplified generator only supports types as mapped by {nameof(CsTestClientGenerator)}.{nameof(TypeBuilder)}");
    }

    public class TypeBuilder : JsonTypeBuilder
    {
        protected override TypeDesc MapType(Type type)
        {
            return MapArrayType(type) ?? MapNullableType(type) ?? new BasicTypeDesc(type, basicType(type));
        }

        private string basicType(Type type)
        {
            if (type == typeof(void)) return "void";
            if (type == typeof(string)) return "string";
            if (type == typeof(int)) return "int";
            if (type == typeof(bool)) return "bool";
            return $"{type.Namespace}.{type.Name}";
        }
    }
}
