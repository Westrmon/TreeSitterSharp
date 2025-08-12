using GitHub.TreeSitter;

namespace TreeSitterSharp.Parser;

internal class Parser : IParserFile
{
    private TSParser _parser;

    public Parser()
    {
        _parser = new TSParser();
    }

    public TSTree ParseFile(string codeText, TSLanguage language)
    {
        _parser.set_language(language);
        return ParseFile(codeText, tree: null);
    }

    public TSTree ParseFile(string codeText, TSTree tree)
    {
        return _parser.parse_string(tree, codeText);
    }

    public void Reset()
        => _parser.reset();

    public void Dispose()
    {
        _parser.Dispose();
    }
}