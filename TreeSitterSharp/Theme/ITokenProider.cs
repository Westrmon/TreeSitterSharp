namespace TreeSitterSharp.Theme;

public interface ITokenProvider
{
    IEnumerable<StyleToken> GetColorTokens();
}