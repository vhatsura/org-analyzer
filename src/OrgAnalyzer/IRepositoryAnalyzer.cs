using Octokit;

namespace OrgAnalyzer;

public interface IRepositoryIssue
{
    string Title { get; }
}

public record RepositoryMetadata(Repository Repository, string? Ownership);

public interface IRepositoryAnalyzer
{
    ValueTask Initialize();

    ValueTask<IRepositoryIssue?> RunAnalysis(RepositoryMetadata repositoryMetadata);
}
