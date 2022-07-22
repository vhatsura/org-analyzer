using System.Text;
using Octokit;
using OrgAnalyzer.Analyzers;
using OrgAnalyzer.Models;

namespace OrgAnalyzer.Fixers;

public class RepositoryCodeOwnersFixer : IRepositoryIssueFixer
{
    private readonly GitHubService _gitHubService;
    private readonly Dictionary<string, Team> _knownTeams = new();
    private string _defaultCodeOwnersContent = string.Empty;

    public IEnumerable<Type> SupportedTypes
    {
        get
        {
            yield return typeof(MissedCodeOwners);
        }
    }

    public RepositoryCodeOwnersFixer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public async ValueTask Initialize()
    {
        var teams = await _gitHubService.OrganizationTeams();

        var stringBuilder = new StringBuilder();

        foreach (var team in teams)
        {
            var codeOwnersPaths = await _gitHubService.TeamCodeOwnersPaths(team.Slug);

            foreach (var codeOwnersPath in codeOwnersPaths)
            {
                stringBuilder.AppendLine($"{codeOwnersPath} @{_gitHubService.Organization}/{team.Slug}");
            }

            _knownTeams.Add(team.Slug, team);
        }

        _defaultCodeOwnersContent = stringBuilder.ToString();
    }

    public async Task<FixIssueResult> FixIssue(IRepositoryIssue issue, RepositoryMetadata repositoryMetadata)
    {
        if (issue is MissedCodeOwners)
        {
            var existedPullRequest = await _gitHubService.CodeOwnersPullRequest(repositoryMetadata.Repository.Id);
            if (existedPullRequest != null) return new FixIssueResult(FixStatus.InProgress, existedPullRequest.HtmlUrl);

            if (repositoryMetadata.Ownership == null)
            {
                return new FixIssueResult(FixStatus.NotFixed, "Repository ownership is not known");
            }

            if (!_knownTeams.TryGetValue(repositoryMetadata.Ownership, out var team))
            {
                return new FixIssueResult(FixStatus.NotFixed,
                    $"Repository ownership is not known: {repositoryMetadata.Ownership}");
            }

            var codeOwnersContent = string.Concat($"*    @{_gitHubService.Organization}/{team.Slug}",
                Environment.NewLine,
                _defaultCodeOwnersContent,
                Environment.NewLine);

            var pullRequest = await _gitHubService.CreateCodeOwners(repositoryMetadata.Repository.Id,
                repositoryMetadata.Repository.DefaultBranch, codeOwnersContent);

            return new FixIssueResult(FixStatus.InProgress, pullRequest.HtmlUrl);
        }

        return new FixIssueResult(FixStatus.NotFixed, null);
    }
}
