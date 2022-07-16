namespace OrgAnalyzer.Analyzers;

public interface IOrganizationIssue
{
    string Title { get; }
}

public interface IOrganizationAnalyzer
{
    ValueTask Initialize();

    ValueTask<IReadOnlyList<IOrganizationIssue>> RunAnalysis();
}
