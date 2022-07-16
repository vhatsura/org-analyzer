using Octokit;
using OrgAnalyzer.Analyzers;

namespace OrgAnalyzer.Fixers;

public class RepositoryAccessFixer : IRepositoryIssueFixer
{
    private readonly GitHubService _gitHubService;
    private readonly IDictionary<string, Team> _knownTeams = new Dictionary<string, Team>();

    public IEnumerable<Type> SupportedTypes
    {
        get
        {
            yield return typeof(MissedTeamAccess);
            yield return typeof(MissedAdminAccess);
        }
    }

    public RepositoryAccessFixer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public async ValueTask Initialize()
    {
        var teams = await _gitHubService.OrganizationTeams();

        foreach (var team in teams)
        {
            _knownTeams.Add(team.Name.ToLowerInvariant(), team);
        }
    }

    public async Task<bool> FixIssue(IRepositoryIssue issue, RepositoryMetadata repositoryMetadata)
    {
        if (issue is MissedTeamAccess missedTeamAccess)
        {
            if (_knownTeams.TryGetValue(missedTeamAccess.Ownership, out var team))
            {
                await _gitHubService.AddTeamToRepository(team.Id, repositoryMetadata.Repository.Name,
                    missedTeamAccess.Permission);
                return true;
            }
        }

        else if (issue is MissedAdminAccess missedAdminAccess)
        {

        }

        return false;
    }
}
