namespace OrgAnalyzer.Analyzers;

public class RepositoryAccessAnalyzer : IRepositoryAnalyzer
{
    private readonly GitHubService _gitHubService;

    public RepositoryAccessAnalyzer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public ValueTask Initialize()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask<IRepositoryIssue?> RunAnalysis(RepositoryMetadata metadata)
    {
        var teams = await _gitHubService.RepositoryTeams(metadata.Repository.Id);

        return null;
    }
}
