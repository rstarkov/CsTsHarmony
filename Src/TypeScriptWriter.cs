using System;
using System.IO;

namespace CsTsApi
{
    public class TypeScriptWriter : IDisposable
    {
        private TextWriter _writer;
        private bool _needIndent = true;
        private int _indentLevel = 0;

        public string IndentTemplate = "    ";

        public TypeScriptWriter(string filepath)
        {
            filepath = Path.GetFullPath(filepath);
            Directory.CreateDirectory(Path.GetDirectoryName(filepath));
            _writer = new StreamWriter(File.Open(filepath, FileMode.Create, FileAccess.Write, FileShare.Read));
        }

        public TypeScriptWriter(TextWriter writer)
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
            private TypeScriptWriter _owner;
            private bool _indent;
            public Indenter(TypeScriptWriter owner, bool indent)
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
}
