namespace CsTsHarmony;

public interface ITypeConverter
{
    IEnumerable<string> GetImports();
    string ConvertToTypeScript(string expr);
    string ConvertFromTypeScript(string expr);
}

public class LambdaTypeConverter : ITypeConverter
{
    public string[] Imports;
    public Func<string, string> ToTypeScript, FromTypeScript;

    string ITypeConverter.ConvertToTypeScript(string expr) => ToTypeScript(expr);
    string ITypeConverter.ConvertFromTypeScript(string expr) => FromTypeScript(expr);
    IEnumerable<string> ITypeConverter.GetImports() => Imports ?? Enumerable.Empty<string>();
}
