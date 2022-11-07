using System;
using System.Collections.Generic;

namespace CsTsHarmony;

public abstract class TypeMapper
{
    public abstract ApiTypeDesc MapType(Type type);
    public abstract IEnumerable<string> GetImports();
    public abstract string ConvertToTypeScript(string expr);
    public abstract string ConvertFromTypeScript(string expr);
}

public class LuxonDateTimeMapper : TypeMapper
{
    public override ApiTypeDesc MapType(Type type)
    {
        if (type != typeof(DateTime))
            return null;
        return new ApiTypeDesc { BasicType = "DateTime", TypeMapper = this };
    }

    public override IEnumerable<string> GetImports()
    {
        return new[] { "import { DateTime } from 'luxon';" };
    }

    public override string ConvertFromTypeScript(string expr)
    {
        return "(" + expr + ").toISO()";
    }

    public override string ConvertToTypeScript(string expr)
    {
        return "DateTime.fromISO(" + expr + ")";
    }
}
