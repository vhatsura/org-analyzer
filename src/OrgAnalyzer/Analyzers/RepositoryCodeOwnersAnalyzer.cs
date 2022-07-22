using Octokit;

namespace OrgAnalyzer.Analyzers;

public record MissedCodeOwners : IRepositoryIssue
{
    public string Title => "Missed code owners configuration";
}

public class RepositoryCodeOwnersAnalyzer : IRepositoryAnalyzer
{
    private readonly GitHubService _gitHubService;

    public RepositoryCodeOwnersAnalyzer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public ValueTask Initialize()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask<IReadOnlyList<IRepositoryIssue>> RunAnalysis(RepositoryMetadata repositoryMetadata)
    {
        try
        {
            var codeOwnersContent =
                await _gitHubService.GetRawContent(repositoryMetadata.Repository.Name, ".github/CODEOWNERS");
        }
        catch (NotFoundException)
        {
            return new[] { new MissedCodeOwners() };
        }

        return Array.Empty<IRepositoryIssue>();
    }
}
