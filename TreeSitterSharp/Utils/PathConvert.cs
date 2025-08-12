using System.Collections.Concurrent;
using System.Text;

namespace TreeSitterSharp.Utils;

// 主要实现对路径的双向转化
// 内部维护一个映射表, ushort->string, 用来存储每一阶段的路径
public static class PathConvert
{
    internal static string ProjectPath
    {
        set
        {
            Count = 1;
            pathMap[0] = value;
            pathMapReverse.Clear();
        }
    }

    private static uint Count = 1;

    private static ConcurrentDictionary<uint, string> pathMap = [];
    private static ConcurrentDictionary<string, uint> pathMapReverse = [];

    public static string GetPath(uint[] path)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < path.Length; i++)
        {
            sb.Append(Path.DirectorySeparatorChar);
            sb.Append(pathMap[path[i]]);
        }
        return Path.Combine(pathMap[0], sb.ToString().TrimStart(Path.DirectorySeparatorChar));
    }

    public static (uint[], string) ConvertPath(string path)
    {
        if (!pathMap.ContainsKey(0))
            throw new Exception("ProjectPath is not set");
        if (!path.StartsWith("ST"))
            path = path[pathMap[0].Length..];
        var pathUnits = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        uint[] pathData = new uint[pathUnits.Length - 1];
        for (int i = 0; i < pathUnits.Length - 1; i++)
        {
            var index = pathMapReverse.GetOrAdd(pathUnits[i], (str) =>
                {
                    var count = Interlocked.Increment(ref Count);
                    pathMap[count] = pathUnits[i];
                    return count;
                });
            pathData[i] = index;
        }
        return (pathData, pathUnits[^1]);
    }
}