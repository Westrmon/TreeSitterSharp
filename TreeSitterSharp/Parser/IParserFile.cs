using GitHub.TreeSitter;

namespace TreeSitterSharp.Parser;

internal interface IParserFile : IDisposable
{
    TSTree ParseFile(string codeText, TSTree tree);

    TSTree ParseFile(string codeText, TSLanguage language);

    void Reset();
}