using Octokit;

namespace OrgAnalyzer.Analyzers;

public record MissedTeamAccess(string Ownership) : IRepositoryIssue
{
    public string Title => $"Missed team access: {Ownership}";
}

public record ExtensiveTeamAccess(string Team, string Permission) : IRepositoryIssue
{
    public string Title => $"Extensive team access: {Team} '{Permission}'";
}

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

    public async ValueTask<IReadOnlyList<IRepositoryIssue>> RunAnalysis(RepositoryMetadata metadata)
    {
        var issues = new List<IRepositoryIssue>();

        var teams = await _gitHubService.RepositoryTeams(metadata.Repository.Id);

        Team? ownershipTeam = null;
        foreach (var team in teams)
        {
            if (metadata.Ownership == null || team.Name.ToLowerInvariant() != metadata.Ownership)
            {
                if (team.Permission.StringValue == "admin")
                {
                    issues.Add(new ExtensiveTeamAccess(team.Name, team.Permission.StringValue));
                }
            }
            else
            {
                ownershipTeam = team;
            }
        }

        if (ownershipTeam == null && metadata.Ownership != null)
        {
            issues.Add(new MissedTeamAccess(metadata.Ownership));
        }

        return issues;
    }
}
