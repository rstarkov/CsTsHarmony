using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace CsTsHarmony;

public class ApiServiceBuilder
{
    public ApiDesc Api = new();
    public IgnoreConfig<Type> IgnoreControllers = new();
    public IgnoreConfig<MethodInfo> IgnoreMethods = new();
    public IgnoreConfig<Type> IgnoreTypes = new();

    public Func<string, string> ControllerRenamer = name => name.Replace("Controller", "");
    public List<ITypeMapper> TypeMappers = new() { new BasicTypeMapper(), new EnumTypeMapper(), new CompositeTypeMapper() };
    public T TypeMapper<T>() where T : ITypeMapper => TypeMappers.OfType<T>().SingleOrDefault();

    public HashSet<Type> TypesToMap = new();

    public List<string> DiagnosticLog = new();

    public void AddControllers(IServiceProvider serviceProvider)
    {
        AddControllers(serviceProvider.GetRequiredService<IActionDescriptorCollectionProvider>());
    }

    public void AddControllers(IActionDescriptorCollectionProvider provider)
    {
        foreach (var cad in provider.ActionDescriptors.Items.Where(ad => ad.AttributeRouteInfo != null).OfType<ControllerActionDescriptor>())
            addControllerActionDescriptor(cad);
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
            ReturnType = referenceType(cad.MethodInfo.ReturnType),
            UrlTemplate = cad.AttributeRouteInfo.Template,
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
                Type = referenceType(p.ParameterType),
                Location = _paramLocations[p.BindingInfo.BindingSource.Id],
                Optional = p is ControllerParameterDescriptor cpd ? cpd.ParameterInfo.HasDefaultValue : false,
            })
            .ToList();
        svc.Methods.Add(md);
        return md;
    }

    private TypeRef referenceType(Type type)
    {
        // this is the only mechanism by which we create a TypeRef
        var tr = new TypeRef();
        tr.RawType = type;

        if (tr.RawType == typeof(Task))
            tr.RawType = typeof(void);
        else if (tr.RawType.IsGenericType && tr.RawType.GetGenericTypeDefinition() == typeof(Task<>))
            tr.RawType = tr.RawType.GetGenericArguments()[0];

        if (tr.RawType.IsGenericType && tr.RawType.GetGenericTypeDefinition() == typeof(ActionResult<>))
            tr.RawType = tr.RawType.GetGenericArguments()[0];
        else if (tr.RawType == typeof(ActionResult) || tr.RawType.IsAssignableTo(typeof(IActionResult)) || tr.RawType.IsAssignableTo(typeof(IConvertToActionResult)))
            tr.RawType = typeof(object);
        // this could be made extensible

        if (tr.RawType.IsArray)
        {
            tr.Array = true;
            tr.ArrayNullable = false;
            tr.RawType = tr.RawType.GetElementType();
        }
        else if (getIEnumerable(tr.RawType, out var elType))
        {
            tr.Array = true;
            tr.ArrayNullable = false;
            tr.RawType = elType;
        }
        // this could be made extensible

        if (tr.RawType.IsGenericType && tr.RawType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            tr.RawType = tr.RawType.GetGenericArguments()[0];
            tr.Nullable = true;
        }

        if (!Api.Types.ContainsKey(tr.RawType))
            TypesToMap.Add(tr.RawType);
        return tr;

        static bool getIEnumerable(Type type, out Type elType)
        {
            if (type == typeof(string))
            {
                elType = null;
                return false;
            }

            if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                elType = type.GetGenericArguments()[0];
                return true;
            }

            var ts = type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)).ToList();
            elType = ts.FirstOrDefault()?.GetGenericArguments()[0];
            return ts.Count == 1;
        }
    }

    public void DiscoverTypes()
    {
        while (TypesToMap.Any())
        {
            var type = TypesToMap.First();
            TypesToMap.Remove(type);

            if (Api.Types.ContainsKey(type))
                continue; // it was manually mapped by the caller before invoking MapTypes
            if (!IgnoreTypes.Include(type))
                continue;

            Api.Types[type] = null; // referenceType relies on the existence of this entry to prevent loops on reference cycles

            foreach (var mapper in TypeMappers)
            {
                var result = mapper.MapType(type, referenceType);
                if (result != null)
                {
                    Api.Types[type] = result;
                    break;
                }
            }

            // if we get here the type remains unmapped and a null entry remains in Api.Types
        }
    }

    public void ApplyTypes()
    {
        // Remove null values from Api.Types as that means the same as not being in the mapping at all
        foreach (var kvp in Api.Types.Where(kvp => kvp.Value == null).ToList())
            Api.Types.Remove(kvp.Key);
        // Populate TypeRefs with the mapped types and ignore (remove) everything in the API that uses unmapped types
        foreach (var s in Api.Services)
        {
            s.Methods.RemoveAll(m => !have(m.ReturnType) || m.Parameters.Any(p => !have(p.Type)));
            foreach (var m in s.Methods)
            {
                m.ReturnType.MappedType = Api.Types[m.ReturnType.RawType];
                foreach (var p in m.Parameters)
                    p.Type.MappedType = Api.Types[p.Type.RawType];
            }
        }
        foreach (var t in Api.Types.Values.OfType<CompositeTypeDesc>())
        {
            t.Properties.RemoveAll(p => !have(p.Type));
            foreach (var p in t.Properties)
                p.Type.MappedType = Api.Types[p.Type.RawType];
            t.Extends.RemoveAll(e => !have(e));
            foreach (var e in t.Extends)
                e.MappedType = Api.Types[e.RawType];
        }

        bool have(TypeRef r) => Api.Types.ContainsKey(r.RawType);
    }
}

public class IgnoreConfig<T> where T : MemberInfo
{
    public HashSet<T> Ignored = new();
    public Func<T, bool> Filter = null;
    public HashSet<string> Attributes = new() { "Newtonsoft.Json.JsonIgnoreAttribute" };

    public bool Include(T value)
    {
        if (Ignored.Contains(value))
            return false;
        if (value.CustomAttributes.Any(ca => Attributes.Contains(ca.AttributeType.FullName)))
        {
            Ignored.Add(value);
            return false;
        }
        if (Filter != null && !Filter(value))
        {
            Ignored.Add(value);
            return false;
        }
        return true;
    }
}
