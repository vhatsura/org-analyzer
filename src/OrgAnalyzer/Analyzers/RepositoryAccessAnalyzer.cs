using Octokit;
using OrgAnalyzer.Extensions;

namespace OrgAnalyzer.Analyzers;

public record MissedTeamAccess(string Ownership, Permission Permission) : IRepositoryIssue
{
    public string Title => $"Missed '{Ownership}' team access with '{Permission}' permission.";
}

public record MissedAdminAccess(string Login) : IRepositoryIssue
{
    public string Title => $"Missed admin access for '{Login}'.";
}

public record ExtensiveTeamAccess(string Team, string Permission) : IRepositoryIssue
{
    public string Title =>
        $"Extensive '{Team}' team access: '{Permission}'.";
}

public record ExtensiveCollaboratorAccess(string Login, string Permission) : IRepositoryIssue
{
    public string Title =>
        $"Extensive '{Login}' collaborator access: '{Permission}'.";
}

public record InvalidTeamAccess
    (string Team, Permission ActualPermission, Permission ExpectedPermission) : IRepositoryIssue
{
    public string Title => $"Invalid '{Team}' team access: '{ActualPermission}' instead of '{ExpectedPermission}'.";
}

public class RepositoryAccessAnalyzer : IRepositoryAnalyzer
{
    private readonly GitHubService _gitHubService;
    private Dictionary<string, string> _parentTeams = new();
    private readonly HashSet<string> _ownerLogins = new();
    private readonly Dictionary<int, HashSet<string>> _teamMaintainers = new();
    private readonly Dictionary<(long RepositoryId, int TeamId), RepositoryPermissions> _teamPermissions = new();

    public RepositoryAccessAnalyzer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public async ValueTask Initialize()
    {
        var teams = await _gitHubService.OrganizationTeams();
        _parentTeams = teams.Where(x => x.Parent != null)
            .ToDictionary(x => x.Name.ToLowerInvariant(), x => x.Parent.Name.ToLowerInvariant());

        var owners = await _gitHubService.OrganizationOwners();

        foreach (var owner in owners)
        {
            _ownerLogins.Add(owner.Login);
        }

        foreach (var team in teams)
        {
            var members = await _gitHubService.TeamMembers(team.Id);

            _teamMaintainers.Add(team.Id, members.Where(x =>
                x.Membership.Role.Value == TeamRole.Maintainer &&
                x.Membership.State.Value == MembershipState.Active).Select(x => x.User.Login).ToHashSet());

            var teamRepositories = await _gitHubService.TeamRepositories(team.Id);

            foreach (var teamRepository in teamRepositories)
            {
                _teamPermissions.Add((teamRepository.Id, team.Id), teamRepository.Permissions);
            }
        }
    }

    public async ValueTask<IReadOnlyList<IRepositoryIssue>> RunAnalysis(RepositoryMetadata metadata)
    {
        var issues = new List<IRepositoryIssue>();
        var teams = await _gitHubService.RepositoryTeams(metadata.Repository.Id);

        await foreach (var issue in TeamAccessIssues(metadata, teams))
        {
            issues.Add(issue);
        }

        var teamsMaintainers = new HashSet<string>();

        foreach (var team in teams)
        {
            if (_teamMaintainers.TryGetValue(team.Id, out var teamMaintainers))
            {
                teamsMaintainers.UnionWith(teamMaintainers);
            }
        }

        // todo: check users for access
        var repositoryCollaborators = await _gitHubService.RepositoryCollaborators(metadata.Repository.Id);

        foreach (var collaborator in repositoryCollaborators)
        {
            if (collaborator.Permissions.Admin && !_ownerLogins.Contains(collaborator.Login) &&
                !teamsMaintainers.Contains(collaborator.Login))
            {
                issues.Add(new ExtensiveCollaboratorAccess(collaborator.Login, "admin"));
            }

            if (teamsMaintainers.Contains(collaborator.Login) && !collaborator.Permissions.Admin)
            {
                issues.Add(new MissedAdminAccess(collaborator.Login));
            }
        }

        return issues;
    }

    private async IAsyncEnumerable<IRepositoryIssue> TeamAccessIssues(RepositoryMetadata metadata,
        IReadOnlyList<Team> teams)
    {
        // todo: refactor this
        Team? ownershipTeam = null;
        Team? parentOwnershipTeam = null;
        string? parentTeamName = null;

        if (metadata.Ownership != null)
        {
            _parentTeams.TryGetValue(metadata.Ownership, out parentTeamName);
        }

        foreach (var team in teams)
        {
            if (!_teamPermissions.TryGetValue((metadata.Repository.Id, team.Id), out var permissions))
            {
                throw new InvalidOperationException();
            }

            var teamPermission = permissions.ToPermission();

            if (metadata.Ownership != null && team.Name.ToLowerInvariant() == metadata.Ownership)
            {
                if (teamPermission != Permission.Maintain)
                {
                    yield return new InvalidTeamAccess(team.Name, teamPermission, Permission.Maintain);
                }

                ownershipTeam = team;
            }
            else if (parentTeamName != null && team.Name.ToLowerInvariant() == parentTeamName)
            {
                if (teamPermission != Permission.Push)
                {
                    yield return new InvalidTeamAccess(team.Name, teamPermission, Permission.Push);
                }

                parentOwnershipTeam = team;
            }
            else
            {
                if (teamPermission != Permission.Pull)
                {
                    yield return new InvalidTeamAccess(team.Name, teamPermission, Permission.Pull);
                }
            }
        }

        if (ownershipTeam == null && metadata.Ownership != null)
        {
            yield return new MissedTeamAccess(metadata.Ownership, Permission.Maintain);
        }

        if (parentTeamName != null && parentOwnershipTeam == null)
        {
            yield return new MissedTeamAccess(parentTeamName, Permission.Push);
        }
    }
}
