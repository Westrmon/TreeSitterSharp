namespace TreeSitterSharp.Scheduler;

public interface IParseProcessor
{
    bool BeforeParse();

    bool StartParse();

    bool EndParse();
}