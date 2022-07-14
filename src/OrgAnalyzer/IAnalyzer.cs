namespace OrgAnalyzer;

public interface IAnalyzerResult
{
}

public interface IAnalyzer<TResult> where TResult : IAnalyzerResult
{
    Task<TResult> RunAnalysis();
}
