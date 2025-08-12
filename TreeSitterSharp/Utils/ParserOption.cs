namespace TreeSitterSharp.Utils;

// 对外开放的配置类, 用于配置解析器, 同时也是存储在项目文件的地方
public class ParserOption
{
    private static ParserOption? instance;
    private static readonly Lock lockObj = new();

    internal ParseConfig Config;

    protected ParserOption()
    { }

    public static ParserOption GetInstance()
    {
        using (lockObj.EnterScope())
        {
            instance ??= new ParserOption();
        }
        return instance;
    }

    public void SetConfig(ParseConfig config)
    {
        Config ??= config;
    }

    private string projectDir = string.Empty;

    public string ProjectDir
    {
        get => projectDir;
        set
        {
            projectDir = value;
            PathConvert.ProjectPath = value;
        }
    }
}