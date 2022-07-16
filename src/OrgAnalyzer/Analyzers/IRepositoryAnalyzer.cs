using Octokit;

namespace OrgAnalyzer.Analyzers;

public interface IRepositoryIssue
{
    string Title { get; }
}

public record RepositoryMetadata(Repository Repository, string? Ownership);

public interface IRepositoryAnalyzer
{
    ValueTask Initialize();

    ValueTask<IReadOnlyList<IRepositoryIssue>> RunAnalysis(RepositoryMetadata repositoryMetadata);
}

public interface IRepositoryIssueFixer
{
    IEnumerable<Type> SupportedTypes { get; }

    ValueTask Initialize();

    Task<bool> FixIssue(IRepositoryIssue issue, RepositoryMetadata repositoryMetadata);
}
