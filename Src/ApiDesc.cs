using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace CsTsApi
{
    public class ApiDesc
    {
        public HashSet<string> Imports = new HashSet<string>() { "import { ApiServiceBase } from './ApiLib';" };
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

    public enum SendCookies { Never = 1, SameOriginOnly, Always };

    public class ApiServiceDesc
    {
        public Type Controller;
        public string Name;
        public SendCookies SendCookies = SendCookies.Never;
        public List<ApiMethodDesc> Methods = new List<ApiMethodDesc>();
    }

    public enum BodyEncoding
    {
        /// <summary>
        ///     At most one body parameter is allowed, and is expected to be of type string, Blob, File or ArrayBuffer /
        ///     ArrayBufferView.</summary>
        Raw = 1,
        /// <summary>At most one body parameter is allowed, and is encoded using JSON.stringify.</summary>
        Json,
        MultipartFormData,
        FormUrlEncoded,
    }

    public class ApiMethodDesc
    {
        public MethodInfo Method;
        public ApiServiceDesc Service;
        public string TsName;
        public ApiTypeDesc TsReturnType;
        /// <summary>This URL is emitted in backticks and so may contain interpolated code.</summary>
        public string UrlPath;
        public List<ApiMethodParameterDesc> Parameters = new List<ApiMethodParameterDesc>();
        public List<string> HttpMethods = new List<string>();
        public BodyEncoding BodyEncoding;
    }

    public enum ParameterLocation
    {
        UrlSegment = 1,
        QueryString,
        RequestBody,
    }

    public class ApiMethodParameterDesc
    {
        public ParameterInfo Parameter;
        public ApiMethodDesc Method;
        public string TsName;
        public ApiTypeDesc TsType;
        public ParameterLocation Location;
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
        public ApiTypeDesc TsType;
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

    public class ApiTypeDesc
    {
        public string BasicType;
        public ApiInterfaceDesc InterfaceType;
        public ApiEnumDesc EnumType;
        public ApiTypeDesc ArrayElementType;
        public bool Nullable;
        public TypeMapper TypeMapper;

        public string GetTypeScript()
        {
            string result;

            if (BasicType != null)
                result = BasicType;
            else if (InterfaceType != null)
                result = InterfaceType.TsName;
            else if (EnumType != null)
                result = EnumType.TsName;
            else if (ArrayElementType != null)
                result = "(" + ArrayElementType.GetTypeScript() + ")[]";
            else
                throw new Exception();

            if (Nullable)
                result += " | null";
            return result;
        }

        public string GetHash()
        {
            return MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(GetTypeScript())).Take(8).Select(b => $"{b:x2}").JoinString();
        }
    }
}
