namespace OrgAnalyzer.Analyzers;

public record MissedOwnershipTopic : IRepositoryIssue
{
    public string Title => "Repository missed ownership topic";
}

public record UnknownOwnershipTopic(string Topic) : IRepositoryIssue
{
    public string Title => $"Unknown ownership topic: {Topic}";
}

public record MissedOrInvalidRepositoryType : IRepositoryIssue
{
    public string Title => "Repository type missed or invalid";
}

public class RepositoryTopicsAnalyzer : IRepositoryAnalyzer
{
    private readonly GitHubService _gitHubService;
    private readonly HashSet<string> _knownTeams = new();

    public RepositoryTopicsAnalyzer(GitHubService gitHubService)
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
        var issues = new List<IRepositoryIssue>();

        if (repositoryMetadata.Ownership == null)
        {
            issues.Add(new MissedOwnershipTopic());
        }
        else if (!_knownTeams.Contains(repositoryMetadata.Ownership))
        {
            issues.Add(new UnknownOwnershipTopic(repositoryMetadata.Ownership));
        }

        if (repositoryMetadata.Type == RepositoryType.Unknown)
        {

        }

        return new ValueTask<IReadOnlyList<IRepositoryIssue>>(issues);
    }
}
