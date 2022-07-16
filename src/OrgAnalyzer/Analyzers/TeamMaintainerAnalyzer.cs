using Octokit;

namespace OrgAnalyzer.Analyzers;

public record MaintainerMissed(string Team) : IOrganizationIssue
{
    public string Title => $"Maintainer for '{Team}' team missed";
}

public record MissedTeamMembers(string Team) : IOrganizationIssue
{
    public string Title => $"Team '{Team}' missed members";
}

public class TeamMaintainerAnalyzer : IOrganizationAnalyzer
{
    private readonly GitHubService _gitHubService;

    public TeamMaintainerAnalyzer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public ValueTask Initialize()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask<IReadOnlyList<IOrganizationIssue>> RunAnalysis()
    {
        var issues = new List<IOrganizationIssue>();

        var teams = await _gitHubService.OrganizationTeams();
        foreach (var team in teams)
        {
            var members = await _gitHubService.TeamMembers(team.Id);

            if (members.Count == 0)
            {
                issues.Add(new MissedTeamMembers(team.Name));
            }
            else if (!members.Any(x =>
                         x.Membership.Role.Value == TeamRole.Maintainer &&
                         x.Membership.State.Value != MembershipState.Pending))
            {
                issues.Add(new MaintainerMissed(team.Name));
            }
        }

        return issues;
    }
}
