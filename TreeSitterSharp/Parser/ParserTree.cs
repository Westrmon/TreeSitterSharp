using GitHub.TreeSitter;
using System.Diagnostics.CodeAnalysis;
using TreeSitterSharp.Utils;

namespace TreeSitterSharp.Parser;

// 需要修改一下路径的存储方式, 由于文件路径大部分都是相似的, 所以采用相对路径+路径映射方式来存储
internal readonly struct ParserTree : IDisposable
{
    internal string FileName { get; }

    internal uint[] FilePaths { get; }

    public TSTree Tree { get; }

    public string FilePath
        => Path.Combine(PathConvert.GetPath(FilePaths), FileName);

    public ParserTree(string filePath, TSTree tree)
    {
        Tree = tree;
        (FilePaths, FileName) = PathConvert.ConvertPath(filePath);
    }

    private bool IsEqual(ParserTree other)
        => FilePaths.Length == other.FilePaths.Length
        && FilePaths.Zip(other.FilePaths, (a, b) => a == b).All(x => x)
        && FileName == other.FileName;

    public bool IsEqual(ushort[] path, string fileName)
        => FilePaths.Length == path.Length
        && FilePaths.Zip(path, (a, b) => a == b).All(x => x)
        && FileName == fileName;

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is ParserTree other)
            return IsEqual(other);
        else
            return false;
    }

    public override int GetHashCode()
        => HashCode.Combine(FilePaths, FileName);

    public override string ToString() => FilePath;

    public void Dispose()
        => Tree.Dispose();
}