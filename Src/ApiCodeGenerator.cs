using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CsTsApi
{
    public class ApiCodeGenerator
    {
        public Func<string> GetServiceImports = () => "import { ApiServiceBase } from './ApiLib.ts';";
        public string ReturnTypeTemplate = "Promise<{0}>";

        private string getName(string fullname) => fullname.Contains(".") ? fullname.Substring(fullname.LastIndexOf('.') + 1) : fullname;
        private string getNamespace(string fullname) => fullname.Contains(".") ? fullname.Substring(0, fullname.LastIndexOf('.')) : null;

        public void OutputTypings(TypeScriptWriter writer, ApiDesc api)
        {
            var namespaces = api.Interfaces.Values.Select(i => getNamespace(i.TsName)).Concat(api.Enums.Values.Select(e => getNamespace(e.TsName))).Distinct().Order();
            foreach (var ns in new string[] { null }.Concat(namespaces))
            {
                var enums = api.Enums.Values.Where(e => getNamespace(e.TsName) == ns).ToList();
                var interfaces = api.Interfaces.Values.Where(i => getNamespace(i.TsName) == ns).ToList();
                if (enums.Count == 0 && interfaces.Count == 0)
                    continue;
                if (ns != null)
                {
                    writer.WriteLine($"namespace {ns} {{");
                    writer.WriteLine();
                }
                using (writer.Indent(ns != null))
                {
                    foreach (var e in enums)
                    {
                        OutputEnum(writer, e);
                        writer.WriteLine();
                    }
                    foreach (var i in interfaces)
                    {
                        OutputInterface(writer, i);
                        writer.WriteLine();
                    }
                }
                if (ns != null)
                    writer.WriteLine("}");
            }
        }

        public void OutputServices(TypeScriptWriter writer, ApiDesc api)
        {
            writer.WriteLine(GetServiceImports());
            writer.WriteLine();
            writer.WriteLine("export class Services {");
            using (writer.Indent())
            {
                foreach (var svc in api.Services.OrderBy(s => s.Name))
                    writer.WriteLine($"public readonly {svc.Name}: {svc.Name}Service;");
                writer.WriteLine();
                writer.WriteLine("public constructor(hostname: string) {");
                using (writer.Indent())
                {
                    foreach (var svc in api.Services.OrderBy(s => s.Name))
                        writer.WriteLine($"this.{svc.Name} = new {svc.Name}Service(hostname);");
                }
                writer.WriteLine("}");
            }
            writer.WriteLine("}");
            writer.WriteLine();
            foreach (var svc in api.Services.OrderBy(s => s.Name))
            {
                OutputService(writer, svc);
                writer.WriteLine();
            }
        }

        protected void OutputEnum(TypeScriptWriter writer, ApiEnumDesc e)
        {
            writer.WriteLine($"type {getName(e.TsName)} = {e.Values.Select(v => v.TsName).Order().JoinString(" | ", "\"", "\"")};");
        }

        protected void OutputInterface(TypeScriptWriter writer, ApiInterfaceDesc i)
        {
            writer.Write($"interface {getName(i.TsName)}");
            if (i.Extends.Any())
            {
                writer.Write(" extends ");
                writer.Write(i.Extends.Select(e => getNamespace(e.TsName) == getNamespace(i.TsName) ? getName(e.TsName) : e.TsName).Order().JoinString(", "));
            }
            writer.WriteLine(" {");
            using (writer.Indent())
            {
                foreach (var prop in i.Properties.OrderBy(p => p.TsName))
                    writer.WriteLine($"{prop.TsName}: {prop.TsType};");
            }
            writer.WriteLine("}");
        }

        protected void OutputService(TypeScriptWriter writer, ApiServiceDesc s)
        {
            writer.WriteLine($"export class {s.Name}Service extends ApiServiceBase {{");
            writer.WriteLine();
            using (writer.Indent())
            {
                writer.WriteLine("public constructor(hostname: string) {");
                using (writer.Indent())
                    writer.WriteLine("this._hostname = (hostname.substr(-1) == '/') ? hostname : hostname + '/';");
                writer.WriteLine("}");
                writer.WriteLine();
                foreach (var method in s.Methods.OrderBy(m => m.TsName))
                {
                    foreach (var httpMethod in method.HttpMethods.Order())
                    {
                        writer.Write($"public {getMethodName(method, httpMethod)}(");
                        bool first = true;
                        foreach (var p in method.Parameters)
                        {
                            if (!first)
                                writer.Write(", ");
                            writer.Write($"{p.TsName}: {p.TsType}");
                            first = false;
                        }
                        writer.WriteLine($"): {string.Format(ReturnTypeTemplate, method.TsReturnType)} {{");
                        using (writer.Indent())
                        {
                            var paramsBody = method.Parameters.ToDictionary(p => p.TsName);
                            var url = Regex.Replace(method.UrlPath, @"{([a-zA-Z0-9]+)}", m =>
                            {
                                if (!paramsBody.ContainsKey(m.Groups[1].Value))
                                    throw new InvalidOperationException($"No matching method parameter found for URL segment \"{m.Value}\", method {method.Method.Name}, controller {s.Controller.FullName}");
                                paramsBody.Remove(m.Groups[1].Value);
                                return "${" + m.Groups[1].Value + "}";
                            });
                            writer.WriteLine($"let url = this._hostname + `{url}`;");
                            writer.WriteLine($"return this.{httpMethod}(url);");
                        }
                        writer.WriteLine("}");
                        writer.WriteLine();
                    }
                }
            }
            writer.WriteLine("}");
        }

        private string getMethodName(ApiMethodDesc method, string httpMethod)
        {
            return method.TsName + (method.HttpMethods.Count == 1 ? "" : httpMethod.Substring(0, 1) + httpMethod.Substring(1).ToLower());
        }
    }
}
