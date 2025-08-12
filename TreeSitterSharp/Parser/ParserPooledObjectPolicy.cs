using Microsoft.Extensions.ObjectPool;

namespace TreeSitterSharp.Parser;

internal class ParserPooledObjectPolicy : IPooledObjectPolicy<IParserFile>
{
    public IParserFile Create()
    {
        return new Parser();
    }

    public bool Return(IParserFile obj)
    {
        obj.Reset();
        return true;
    }
}