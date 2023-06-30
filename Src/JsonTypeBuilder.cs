using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CsTsHarmony;

public class JsonTypeBuilder
{
    public ApiDesc Api; // TODO: remove

    public IgnoreConfig<Type> IgnoreTypes = new();
    public List<ITypeMapper> TypeMappers = new() { new BasicTypeMapper(), new EnumTypeMapper(), new CompositeTypeMapper() };
    public T TypeMapper<T>() where T : ITypeMapper => TypeMappers.OfType<T>().SingleOrDefault();
    public HashSet<Type> TypesToMap = new();

    public TypeRef ReferenceType(Type type)
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
                var result = mapper.MapType(type, ReferenceType);
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
