using Octokit;
using OrgAnalyzer.Analyzers;
using OrgAnalyzer.Models;

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
            yield return typeof(InvalidTeamAccess);
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

    public async Task<FixIssueResult> FixIssue(IRepositoryIssue issue, RepositoryMetadata repositoryMetadata)
    {
        if (issue is MissedTeamAccess missedTeamAccess)
        {
            if (_knownTeams.TryGetValue(missedTeamAccess.Ownership.ToLowerInvariant(), out var team))
            {
                await _gitHubService.AddOrUpdateTeamForRepository(team.Id, repositoryMetadata.Repository.Name,
                    missedTeamAccess.Permission);

                return new FixIssueResult(FixStatus.Fixed, null);
            }
        }

        else if (issue is MissedAdminAccess missedAdminAccess)
        {
        }

        else if (issue is InvalidTeamAccess invalidTeamAccess)
        {
            if (_knownTeams.TryGetValue(invalidTeamAccess.Team.ToLowerInvariant(), out var team))
            {
                await _gitHubService.AddOrUpdateTeamForRepository(team.Id, repositoryMetadata.Repository.Name,
                    invalidTeamAccess.ExpectedPermission);

                return new FixIssueResult(FixStatus.Fixed, null);
            }
        }

        return new FixIssueResult(FixStatus.NotFixed, null);
    }
}
