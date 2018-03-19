using System;
using System.Collections.Generic;
using System.Reflection;

namespace CsTsApi
{
    public class ApiDesc
    {
        /// <summary>The TypeScript type to use for <c>DateTime</c> properties / parameters.</summary>
        public string DateTimeType = "string";
        /// <summary>
        ///     If <c>true</c>, " | null" is appended to every nullable type. Otherwise, " | null" is only appended to
        ///     nullable value types.</summary>
        public bool StrictNulls = false;
        /// <summary>
        ///     Source assemblies for the API data transfer interfaces. When a method requires certain types, which may
        ///     recursively require other types, this set of assemblies is used to determine whether to include said type.</summary>
        public HashSet<Assembly> Assemblies = new HashSet<Assembly>();

        public List<ApiServiceDesc> Services = new List<ApiServiceDesc>();
        public Dictionary<Type, ApiInterfaceDesc> Interfaces = new Dictionary<Type, ApiInterfaceDesc>();
        public Dictionary<Type, ApiEnumDesc> Enums = new Dictionary<Type, ApiEnumDesc>();
    }

    public class ApiServiceDesc
    {
        public Type Controller;
        public string Name;
        public List<ApiMethodDesc> Methods = new List<ApiMethodDesc>();
    }

    public class ApiMethodDesc
    {
        public MethodInfo Method;
        public ApiServiceDesc Service;
        public string TsName;
        public string TsReturnType;
        public string UrlPath;
        public List<ApiMethodParameterDesc> Parameters = new List<ApiMethodParameterDesc>();
        public List<string> HttpMethods = new List<string>();
    }

    public class ApiMethodParameterDesc
    {
        public ParameterInfo Parameter;
        public ApiMethodDesc Method;
        public string TsName;
        public string TsType;
    }

    public class ApiInterfaceDesc
    {
        public Type Type;
        public string TsName;
        public List<ApiInterfaceDesc> Extends = new List<ApiInterfaceDesc>();
        public List<ApiPropertyDesc> Properties = new List<ApiPropertyDesc>();
    }

    public class ApiPropertyDesc
    {
        public MemberInfo Member;
        public string TsName;
        public string TsType;
    }

    public class ApiEnumDesc
    {
        public Type Type;
        public bool IsFlags;
        public string TsName;
        public List<ApiEnumValueDesc> Values = new List<ApiEnumValueDesc>();
    }

    public class ApiEnumValueDesc
    {
        public long NumericValue;
        public string TsName;
    }
}
