namespace CsTsHarmony;

public class CodeWriter : IDisposable
{
    private TextWriter _writer;
    private bool _needIndent = true;
    private int _indentLevel = 0;

    public string IndentTemplate = "    ";

    public CodeWriter(string filepath)
    {
        filepath = Path.GetFullPath(filepath);
        Directory.CreateDirectory(Path.GetDirectoryName(filepath));
        _writer = new StreamWriter(File.Open(filepath, FileMode.Create, FileAccess.Write, FileShare.Read));
    }

    public CodeWriter(TextWriter writer)
    {
        _writer = writer;
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _writer = null;
    }

    private class Indenter : IDisposable
    {
        private CodeWriter _owner;
        private bool _indent;

        public Indenter(CodeWriter owner, bool indent)
        {
            _owner = owner;
            _indent = indent;
            if (indent)
                _owner._indentLevel++;
        }

        void IDisposable.Dispose()
        {
            if (_owner != null && _indent)
                _owner._indentLevel--;
            _owner = null;
        }
    }

    public IDisposable Indent(bool indent = true)
    {
        return new Indenter(this, indent);
    }

    public void Write(string str)
    {
        if (_needIndent && str != null && str != "")
            for (int i = 0; i < _indentLevel; i++)
                _writer.Write(IndentTemplate);
        _needIndent = false;
        _writer.Write(str);
    }

    public void WriteLine(string str = null)
    {
        Write(str);
        _writer.WriteLine();
        _needIndent = true;
    }
}

public class TypeScriptWriter : CodeWriter
{
    public TypeScriptWriter(string filepath) : base(filepath)
    {
    }

    public TypeScriptWriter(TextWriter writer) : base(writer)
    {
    }

    public static string TypeSignature(TypeDesc type, string fromNamespace)
    {
        if (type is BasicTypeDesc bt)
            return bt.TsType;
        else if (type is EnumTypeDesc et)
            return fromNamespace == et.TsNamespace ? et.TsName : $"{et.TsNamespace}.{et.TsName}";
        else if (type is CompositeTypeDesc ct)
            return fromNamespace == ct.TsNamespace ? ct.TsName : $"{ct.TsNamespace}.{ct.TsName}";
        else if (type is NullableTypeDesc nt)
            return $"{TypeSignature(nt.ElementType, fromNamespace)} | null";
        else if (type is ArrayTypeDesc at)
            return $"{ParenthesizeType(TypeSignature(at.ElementType, fromNamespace))}[]";
        else
            throw new InvalidOperationException("ilsauwhf");
    }

    public static string ParenthesizeType(string type) => type.Any(c => c == ' ' || c == '|' || c == '[') ? $"({type})" : type;
}
