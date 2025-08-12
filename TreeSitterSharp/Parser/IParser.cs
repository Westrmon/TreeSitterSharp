namespace TreeSitterSharp.Parser;

public interface IParser : IDisposable

{
    IQueryTree ParseFile(string filePath);

    IQueryTree ParseFile(string filePath, string suffix);

    IQueryTree ParseFile(StreamReader reader, string suffix);

    (IQueryTree Query, string VirtualPath) ParseString(string code, string suffix);

    IQueryTree? GetQuery(string filePath);

    IQueryTree? GetQuery(string filePath, string suffix);

    IQueryTree? GetQuery(int hashCode);

    void DisposeLanguage(string suffix);
}