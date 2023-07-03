using System.Reflection;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;

namespace CsTsHarmony;

public class AspCoreMvcBuilder
{
    public ApiDesc Api = new();
    public JsonTypeBuilder TypeBuilder;
    public IgnoreConfig<Type> IgnoreControllers = new();
    public IgnoreConfig<MethodInfo> IgnoreMethods = new();

    public Func<string, string> ControllerRenamer = name => name.Replace("Controller", "");

    public List<string> DiagnosticLog = new();

    public void AddControllers(IServiceProvider serviceProvider)
    {
        AddControllers(serviceProvider.GetRequiredService<IActionDescriptorCollectionProvider>());
    }

    public void AddControllers(IActionDescriptorCollectionProvider provider)
    {
        foreach (var cad in provider.ActionDescriptors.Items.Where(ad => ad.AttributeRouteInfo != null).OfType<ControllerActionDescriptor>())
            addControllerActionDescriptor(cad);

        // Rename duplicate/overloaded methods
        foreach (var s in Api.Services)
        {
            var existing = s.Methods.Select(m => m.TsName).ToHashSet();
            foreach (var grp in s.Methods.GroupBy(g => g.TsName).Where(g => g.Count() > 1))
            {
                var methods = grp.ToList();
                int num = 1;
                foreach (var method in methods)
                {
                    while (existing.Contains($"{grp.Key}_{num}"))
                        num++;
                    method.TsName = $"{grp.Key}_{num}";
                    existing.Add(method.TsName);
                }
            }
        }
    }

    private ServiceDesc addController(Type controllerType)
    {
        if (!IgnoreControllers.Include(controllerType))
            return null;
        var svc = Api.Services.FirstOrDefault(s => s.ControllerType == controllerType);
        if (svc != null)
            return svc;
        svc = new ServiceDesc(controllerType);
        svc.TsName = ControllerRenamer(controllerType.Name);
        Api.Services.Add(svc);
        return svc;
    }

    private static Dictionary<string, ParameterLocation> _paramLocations = new()
    {
        ["Path"] = ParameterLocation.UrlSegment,
        ["Query"] = ParameterLocation.QueryString,
        ["Body"] = ParameterLocation.RequestBody,
    };

    private MethodDesc addControllerActionDescriptor(ControllerActionDescriptor cad)
    {
        if (!IgnoreMethods.Include(cad.MethodInfo))
            return null;
        var controllerType = cad.ControllerTypeInfo.AsType();
        var svc = addController(controllerType);
        if (svc == null)
            return null;
        var md = new MethodDesc(cad.MethodInfo, svc)
        {
            TsName = cad.ActionName,
            HttpMethods = cad.ActionConstraints?.OfType<HttpMethodActionConstraint>().FirstOrDefault()?.HttpMethods.ToList(),
            ReturnType = TypeBuilder.AddType(cad.MethodInfo.ReturnType),
            UrlTemplate = TemplateParser.Parse(cad.AttributeRouteInfo.Template),
            BodyEncoding = BodyEncoding.Json,
        };
        var badParam = cad.Parameters.FirstOrDefault(p => p.BindingInfo.BindingSource.IsFromRequest && !_paramLocations.ContainsKey(p.BindingInfo.BindingSource.Id));
        if (badParam != null)
        {
            DiagnosticLog.Add($"Skipping method {cad.MethodInfo.Name} on {controllerType.FullName} because parameter {badParam.Name} has BindingSource.Id={badParam.BindingInfo.BindingSource.Id}");
            return null;
        }
        md.Parameters = cad.Parameters
            .Where(p => p.BindingInfo.BindingSource.IsFromRequest)
            .Select(p => new MethodParameterDesc(md)
            {
                TsName = p.Name,
                RequestName = p.Name,
                Type = TypeBuilder.AddType(p.ParameterType),
                Location = _paramLocations[p.BindingInfo.BindingSource.Id],
                Optional = p is ControllerParameterDescriptor cpd ? cpd.ParameterInfo.HasDefaultValue : false,
            })
            .ToList();
        svc.Methods.Add(md);
        return md;
    }
}
