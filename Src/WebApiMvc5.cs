using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace CsTsApi
{
    public class WebApiMvc5ServiceBuilder : GenericControllerServiceBuilder
    {
        public WebApiMvc5ServiceBuilder(ApiDesc api) : base(api) { }

        protected override bool IsSupportedController(Type controller)
        {
            return controller.SelectChain(t => t.BaseType).Any(t => t.FullName == "System.Web.Http.ApiController");
        }

        protected override IEnumerable<MethodInfo> GetMethods(Type controller)
        {
            return controller.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        }

        protected override ApiMethodDesc GetMethodDesc(ApiServiceDesc service, MethodInfo method)
        {
            var controllerRoute = service.Controller.GetAttributes("System.Web.Http.RouteAttribute").SingleOrDefault();
            var controllerRoutePrefix = service.Controller.GetAttributes("System.Web.Http.RoutePrefixAttribute").SingleOrDefault();
            var methodRoute = method.GetAttributes("System.Web.Http.RouteAttribute").SingleOrDefault();
            if (controllerRoute == null && methodRoute == null)
                return null;
            var iHttpMethod = "System.Web.Http.Controllers.IActionHttpMethodProvider";
            var httpMethodsAttr = method.GetAttributesByInterface(iHttpMethod).SingleOrDefault();
            if (httpMethodsAttr == null)
                return null;
            
            var m = new ApiMethodDesc();
            m.Method = method;
            m.Service = service;
            m.HttpMethods = ((IEnumerable<object>) httpMethodsAttr.ReadProperty("HttpMethods", Reflection.KnownTypes[iHttpMethod])).Select(hm => ((string) hm.ReadProperty("Method")).ToUpper()).ToList();
            m.BodyEncoding = BodyEncoding.Json;
            m.TsName = method.Name;
            m.TsReturnType = MapType(method.ReturnType);
            if (m.TsReturnType == null)
                throw new InvalidOperationException($"Cannot map return type {method.ReturnType.FullName} of method {method.Name}, controller {service.Controller.FullName}");
            foreach (var par in method.GetParameters())
            {
                var p = new ApiMethodParameterDesc();
                m.Parameters.Add(p);
                p.Method = m;
                p.Parameter = par;
                p.TsName = par.Name;
                p.TsType = MapType(par.ParameterType);
                if (p.TsType == null)
                    throw new InvalidOperationException($"Cannot map parameter type {par.ParameterType.FullName} of parameter {par.Name}, method {method.Name}, controller {service.Controller.FullName}");
                p.Location = IsUrlParameter(p) ? ParameterLocation.QueryString : ParameterLocation.RequestBody;
            }
            m.UrlPath = "/";
            if (controllerRoutePrefix != null)
                m.UrlPath += (string) controllerRoutePrefix.ReadProperty("Prefix") + "/";
            if (methodRoute != null)
                m.UrlPath += (string) methodRoute.ReadProperty("Template");
            else // controllerRoute != null
                m.UrlPath += (string) controllerRoute.ReadProperty("Template");
            m.UrlPath = m.UrlPath.Substring(1);

            var pars = m.Parameters.ToDictionary(p => p.TsName);
            m.UrlPath = Regex.Replace(m.UrlPath, @"{([a-zA-Z0-9]+)}", match =>
            {
                var parName = match.Groups[1].Value;
                if (!pars.ContainsKey(parName))
                    throw new InvalidOperationException($"No matching method parameter found for URL segment \"{match.Value}\", method {method.Name}, controller {service.Controller.FullName}");
                pars[parName].Location = ParameterLocation.UrlSegment;
                return "${encodeURIComponent('' + " + parName + ")}";
            });

            return m;
        }

        protected virtual bool IsUrlParameter(ApiMethodParameterDesc p)
        {
            // https://docs.microsoft.com/en-us/aspnet/web-api/overview/formats-and-model-binding/parameter-binding-in-aspnet-web-api
            if (p.Parameter.GetCustomAttributes().Any(a => a.GetType().Name == "FromBody"))
                return false;
            var t = p.Parameter.ParameterType;
            if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t == typeof(DateTime) || t == typeof(TimeSpan) || t == typeof(Guid))
                return true;
            if (p.Parameter.GetCustomAttributes().Any(a => a.GetType().Name == "FromUri"))
                throw new NotSupportedException($"[FromUri] complex-type parameters are not supported: parameter {p.Parameter.Name}, method {p.Method.Method.Name}, controller {p.Method.Service.Controller.FullName}");
            if (t.GetCustomAttributes().Any(a => a.GetType().Name == "TypeConverter")) // [FromBody] is one possible work around for this limitation
                throw new NotSupportedException($"Complex-type parameters with type converters are not supported (add [FromBody]): parameter {p.Parameter.Name}, method {p.Method.Method.Name}, controller {p.Method.Service.Controller.FullName}");
            return false;
        }
    }
}
