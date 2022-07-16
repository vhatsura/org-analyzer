namespace OrgAnalyzer.Analyzers;

public record MissedOwnershipTopic : IRepositoryIssue
{
    public string Title => "Repository missed ownership topic";
}

public record UnknownOwnershipTopic(string Topic) : IRepositoryIssue
{
    public string Title => $"Unknown ownership topic: {Topic}";
}

public class RepositoryOwnershipTopicAnalyzer : IRepositoryAnalyzer
{
    private readonly GitHubService _gitHubService;
    private readonly HashSet<string> _knownTeams = new();

    public RepositoryOwnershipTopicAnalyzer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public async ValueTask Initialize()
    {
        var teams = await _gitHubService.OrganizationTeams();

        foreach (var team in teams)
        {
            _knownTeams.Add(team.Name.ToLowerInvariant());
        }
    }

    public ValueTask<IReadOnlyList<IRepositoryIssue>> RunAnalysis(RepositoryMetadata repositoryMetadata)
    {
        if (repositoryMetadata.Ownership == null)
        {
            return new ValueTask<IReadOnlyList<IRepositoryIssue>>(new IRepositoryIssue[]
            {
                new MissedOwnershipTopic()
            });
        }

        return new ValueTask<IReadOnlyList<IRepositoryIssue>>(
            !_knownTeams.Contains(repositoryMetadata.Ownership)
                ? new IRepositoryIssue[] { new UnknownOwnershipTopic(repositoryMetadata.Ownership) }
                : Array.Empty<IRepositoryIssue>());
    }
}
