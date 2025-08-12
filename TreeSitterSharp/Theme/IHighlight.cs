using TreeSitterSharp.Parser;

namespace TreeSitterSharp.Theme;

public interface IHighlight
{
    int MaxCacheFile { get; set; }

    /// <summary>
    /// Get the color of the code file
    /// <remark>(Must be at least 10ms apart)</remark>
    /// </summary>
    /// <param name="filePath">File Path</param>
    /// <returns></returns>
    IEnumerable<StyleToken> GetCodeFileColor(string filePath);

    IEnumerable<StyleToken> GetCodeColor(IQueryTree query, string filePath);

    void GetColor(ref StyleToken token);
}