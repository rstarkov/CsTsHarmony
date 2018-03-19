using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CsTsApi
{
    public abstract class ApiServiceBuilder
    {
        protected ApiDesc Api { get; private set; }

        public ApiServiceBuilder(ApiDesc api)
        {
            Api = api;
        }

        /// <summary>
        ///     Adds an assembly to the list of API source assemblies. See <see cref="ApiDesc.Assemblies"/> for more details.</summary>
        public void AddAssembly(Assembly assy)
        {
            Api.Assemblies.Add(assy);
        }

        /// <summary>
        ///     Maps a .NET type to a TypeScript type spec. Returns null if the specified type can't be mapped. The meaning of
        ///     non-mappable types varies depending on the caller; it might be treated as an error or as a signal to ignore a
        ///     certain property or skip a certain base type.</summary>
        protected virtual string MapType(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
                return MapType(type.GetGenericArguments()[0]);

            string result;

            bool nullable = Api.StrictNulls ? !type.IsValueType : false;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                nullable = true;
                type = type.GetGenericArguments()[0];
            }
            if (type == typeof(object))
                result = "any";
            else if (type == typeof(string))
                result = "string";
            else if (type == typeof(int) || type == typeof(double) || type == typeof(decimal))
                result = "number";
            else if (type == typeof(DateTime))
                result = Api.DateTimeType;
            else if (type.IsArray)
            {
                var elType = MapType(type.GetElementType());
                if (elType == null)
                    return null;
                result = "(" + elType + ")[]";
            }
            else if (getIEnumerable(type, out var elT))
            {
                var elType = MapType(elT);
                if (elType == null)
                    return null;
                result = "(" + elType + ")[]";
            }
            else if (type.IsEnum)
            {
                if (!AddEnum(type))
                    return null;
                result = Api.Enums[type].TsName;
            }
            else
            {
                if (!AddInterface(type))
                    return null;
                result = Api.Interfaces[type].TsName;
            }

            if (nullable)
                result += " | null";

            return result;
        }

        private bool getIEnumerable(Type type, out Type elType)
        {
            if (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                elType = type.GetGenericArguments()[0];
                return true;
            }
            var ts = type.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)).ToList();
            elType = ts.FirstOrDefault()?.GetGenericArguments()[0];
            return ts.Count == 1;
        }

        /// <summary>
        ///     Adds an enum for the specified type to the set of API declarations. See <see cref="AddInterface(Type)"/> for
        ///     additional notes.</summary>
        protected virtual bool AddEnum(Type type)
        {
            if (Api.Enums.ContainsKey(type))
                return true;
            if (!ShouldAddType(type))
                return false;
            var en = new ApiEnumDesc();
            Api.Enums[type] = en;
            en.Type = type;
            en.TsName = type.FullName;
            en.IsFlags = type.GetCustomAttribute<FlagsAttribute>() != null;
            foreach (var val in Enum.GetValues(type))
            {
                var desc = new ApiEnumValueDesc();
                desc.NumericValue = Convert.ToInt64(val);
                desc.TsName = Enum.GetName(type, val);
                en.Values.Add(desc);
            }
            return true;
        }

        /// <summary>
        ///     Adds an interface for the specified type to the set of API declarations. May be called multiple times for the
        ///     same type with no ill effects. Recursively adds all additional interfaces required by the one being added.
        ///     Returns true if an interface will exist in TypeScript, or false when it's not possible or desirable to expose
        ///     this type in TypeScript. This logic is based entirely on whether the type is contained in one of the
        ///     whitelisted assemblies (see <see cref="ApiDesc.Assemblies"/> and <see cref="AddAssembly(Assembly)"/>), but can
        ///     be fully customized by overriding this method.</summary>
        protected virtual bool AddInterface(Type type)
        {
            if (Api.Interfaces.ContainsKey(type))
                return true;
            if (!ShouldAddType(type))
                return false;
            var iface = new ApiInterfaceDesc();
            Api.Interfaces[type] = iface;
            iface.Type = type;
            iface.TsName = type.FullName;
            if (type.BaseType != null && AddInterface(type.BaseType))
                iface.Extends.Add(Api.Interfaces[type.BaseType]);
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var ptype = MapType(prop.PropertyType);
                if (ptype == null)
                    continue;
                iface.Properties.Add(new ApiPropertyDesc { Member = prop, TsType = ptype, TsName = prop.Name });
            }
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var ftype = MapType(field.FieldType);
                if (ftype == null)
                    continue;
                iface.Properties.Add(new ApiPropertyDesc { Member = field, TsType = ftype, TsName = field.Name });
            }
            return true;
        }

        protected virtual bool ShouldAddType(Type type)
        {
            return Api.Assemblies.Contains(type.Assembly);
        }
    }

    public abstract class GenericControllerServiceBuilder : ApiServiceBuilder
    {
        public Func<Type, string> GetServiceName = (Type c) => c.Name.EndsWith("Controller") ? c.Name.Substring(0, c.Name.Length - 10) : c.Name;

        public GenericControllerServiceBuilder(ApiDesc api) : base(api) { }

        public virtual ApiServiceDesc AddService(Type controller)
        {
            if (!IsSupportedController(controller))
                throw new Exception($"Unsupported controller type: {controller.FullName}");

            var service = new ApiServiceDesc();
            Api.Services.Add(service);
            AddAssembly(controller.Assembly);
            service.Controller = controller;
            service.Name = GetServiceName(controller);

            foreach (var method in GetMethods(controller))
            {
                var desc = GetMethodDesc(service, method);
                if (desc == null)
                    continue;
                service.Methods.Add(desc);
            }

            PostProcessService(service);

            return service;
        }

        protected virtual void PostProcessService(ApiServiceDesc service)
        {
        }

        protected abstract bool IsSupportedController(Type controller);
        protected abstract IEnumerable<MethodInfo> GetMethods(Type controller);
        protected abstract ApiMethodDesc GetMethodDesc(ApiServiceDesc service, MethodInfo method);
    }
}
