﻿using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;

namespace CsTsHarmony;

public class AspCoreMvcBuilder
{
    public List<ServiceDesc> Services = new();
    public IgnoreConfig<Type> IgnoreControllers = new();
    public IgnoreConfig<MethodInfo> IgnoreMethods = new();
    public Func<Type, string> GetFetcher = t => t == typeof(void) ? "fetchVoid" : t == typeof(string) ? "fetchString" : t == typeof(FileResult) ? "fetchFile" : "fetchJson";
    public Func<string, string> ControllerRenamer = name => name.Replace("Controller", "");
    public List<string> DiagnosticLog = new();

    private ITypeBuilder _typeBuilder;

    public AspCoreMvcBuilder(ITypeBuilder typeBuilder)
    {
        _typeBuilder = typeBuilder;
    }

    public void AddControllers(IServiceProvider serviceProvider)
    {
        AddControllers(serviceProvider.GetRequiredService<IActionDescriptorCollectionProvider>());
    }

    public void AddControllers(IActionDescriptorCollectionProvider provider)
    {
        foreach (var cad in provider.ActionDescriptors.Items.Where(ad => ad.AttributeRouteInfo != null).OfType<ControllerActionDescriptor>())
            addControllerActionDescriptor(cad);
        RenameDuplicateMethods();
    }

    public void RenameDuplicateMethods()
    {
        // this is needed primarily because Typescript doesn't have overloading, but also because a single method may allow multiple HTTP methods
        foreach (var s in Services)
        {
            var existing = s.Methods.Select(m => m.TgtName).ToHashSet();
            string unique(string name)
            {
                if (!existing.Contains(name))
                    return name;
                int num = 1;
                while (existing.Contains($"{name}_{num}"))
                    num++;
                return $"{name}_{num}";
            }

            foreach (var grp in s.Methods.GroupBy(m => m.TgtName).Where(g => g.Count() > 1))
                foreach (var method in grp)
                {
                    method.TgtName = unique(method.TgtName + method.HttpMethod[..1].ToUpper() + method.HttpMethod[1..].ToLower());
                    existing.Add(method.TgtName);
                }
        }
    }

    private ServiceDesc addController(Type controllerType)
    {
        if (!IgnoreControllers.Include(controllerType))
            return null;
        var svc = Services.FirstOrDefault(s => s.ControllerType == controllerType);
        if (svc != null)
            return svc;
        svc = new ServiceDesc(controllerType);
        svc.TgtName = ControllerRenamer(controllerType.Name);
        Services.Add(svc);
        return svc;
    }

    private static Dictionary<string, ParameterLocation> _paramLocations = new()
    {
        ["Path"] = ParameterLocation.UrlSegment,
        ["Query"] = ParameterLocation.QueryString,
        ["Body"] = ParameterLocation.RequestBody,
    };

    private void addControllerActionDescriptor(ControllerActionDescriptor cad)
    {
        if (!IgnoreMethods.Include(cad.MethodInfo))
            return;
        var controllerType = cad.ControllerTypeInfo.AsType();
        var svc = addController(controllerType);
        if (svc == null)
            return;
        foreach (var httpMethod in cad.ActionConstraints?.OfType<HttpMethodActionConstraint>().FirstOrDefault()?.HttpMethods)
        {
            var md = new MethodDesc(cad.MethodInfo, svc)
            {
                TgtName = cad.ActionName,
                HttpMethod = httpMethod,
                ReturnType = _typeBuilder.AddType(cad.MethodInfo.ReturnType),
                UrlTemplate = TemplateParser.Parse(cad.AttributeRouteInfo.Template),
                BodyEncoding = BodyEncoding.Json,
                Fetcher = GetFetcher(HarmonyUtil.UnwrapType(cad.MethodInfo.ReturnType, preserveActionResults: true)),
            };
            var badParam = cad.Parameters.FirstOrDefault(p => p.BindingInfo.BindingSource.IsFromRequest && !_paramLocations.ContainsKey(p.BindingInfo.BindingSource.Id));
            if (badParam != null)
            {
                DiagnosticLog.Add($"Skipping method {cad.MethodInfo.Name} ({httpMethod}) on {controllerType.FullName} because parameter {badParam.Name} has BindingSource.Id={badParam.BindingInfo.BindingSource.Id}");
                continue;
            }
            md.Parameters = cad.Parameters
                .Where(p => p.BindingInfo.BindingSource.IsFromRequest)
                .Select(p => (ControllerParameterDescriptor)p)
                .Select(p => new MethodParameterDesc(p.ParameterInfo, md)
                {
                    TgtName = p.Name,
                    RequestName = p.Name,
                    Type = _typeBuilder.AddType(p.ParameterType),
                    Location = _paramLocations[p.BindingInfo.BindingSource.Id],
                    Optional = p.ParameterInfo.HasDefaultValue,
                })
                .ToList();
            svc.Methods.Add(md);
        }
    }
}
