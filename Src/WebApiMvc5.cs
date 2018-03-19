using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
            }
            m.UrlPath = "/";
            if (controllerRoutePrefix != null)
                m.UrlPath += (string) controllerRoutePrefix.ReadProperty("Prefix") + "/";
            if (methodRoute != null)
                m.UrlPath += (string) methodRoute.ReadProperty("Template");
            else // controllerRoute != null
                m.UrlPath += (string) controllerRoute.ReadProperty("Template");

            return m;
        }
    }
}
