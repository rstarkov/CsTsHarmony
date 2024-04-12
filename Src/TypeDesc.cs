namespace CsTsHarmony;

public abstract class TypeDesc
{
    public Type SrcType { get; }

    public TypeDesc(Type srcType)
    {
        SrcType = srcType;
    }
}

public class BasicTypeDesc : TypeDesc
{
    public string TgtType;
    public override string ToString() => $"{TgtType} ({SrcType.Name})";
    public ITypeConverter TsConverter;

    public BasicTypeDesc(Type srcType, string tgtType) : base(srcType)
    {
        TgtType = tgtType;
    }
}

public class ArrayTypeDesc : TypeDesc
{
    public TypeDesc ElementType;

    public ArrayTypeDesc(Type srcType, TypeDesc elementType) : base(srcType)
    {
        ElementType = elementType;
    }

    public override string ToString() => $"{ElementType}[]";
}

public class NullableTypeDesc : TypeDesc
{
    public TypeDesc ElementType;

    public NullableTypeDesc(Type srcType, TypeDesc elementType) : base(srcType)
    {
        ElementType = elementType;
    }

    public override string ToString() => $"{ElementType} (nullable)";
}

public interface IDeclaredTypeDesc
{
    string TgtName { get; set; }
    string TgtNamespace { get; set; }
}

public class EnumTypeDesc : TypeDesc, IDeclaredTypeDesc
{
    public string TgtName { get; set; }
    public string TgtNamespace { get; set; }
    public List<EnumValueDesc> Values = new();
    public bool IsFlags;

    public override string ToString() => $"{SrcType.FullName} (enum)";

    public EnumTypeDesc(Type srcType) : base(srcType)
    {
        TgtName = srcType.Name;
        TgtNamespace = srcType.Namespace;
    }
}

public class EnumValueDesc
{
    public string Name;
    public long Value;

    public EnumTypeDesc Type { get; }

    public override string ToString() => $"{Name} = {Value}";

    public EnumValueDesc(EnumTypeDesc type)
    {
        Type = type;
    }
}

public class CompositeTypeDesc : TypeDesc, IDeclaredTypeDesc
{
    public string TgtName { get; set; }
    public string TgtNamespace { get; set; }
    public List<PropertyDesc> Properties = new();
    public List<CompositeTypeDesc> Extends = new();
    /// <summary>If not null, Base is also present in <see cref="Extends"/>.</summary>
    public CompositeTypeDesc Base { get; set; }

    public override string ToString() => $"{SrcType.FullName} (composite)";

    public CompositeTypeDesc(Type srcType) : base(srcType)
    {
        TgtName = srcType.Name;
        TgtNamespace = srcType.Namespace;
    }
}

public class PropertyDesc
{
    public string Name;
    public TypeDesc Type; // NullableTypeDesc gets unwrapped, so the top level is never a NullableTypeDesc
    public bool? Nullable; // null means unknown, which only happens for reference types

    public override string ToString() => $"{Name}: {Type}";
}
