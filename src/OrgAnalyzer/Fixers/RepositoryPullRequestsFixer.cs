using Octokit;
using OrgAnalyzer.Analyzers;
using OrgAnalyzer.Models;

namespace OrgAnalyzer.Fixers;

public class RepositoryPullRequestsFixer : IRepositoryIssueFixer
{
    private readonly GitHubService _gitHubService;
    private readonly Dictionary<string, Team> _knownTeams = new();

    public IEnumerable<Type> SupportedTypes
    {
        get
        {
            yield return typeof(MissedReviewersOnPullRequest);
        }
    }

    public RepositoryPullRequestsFixer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public async ValueTask Initialize()
    {
        var teams = await _gitHubService.OrganizationTeams();

        foreach (var team in teams)
        {
            _knownTeams.Add(team.Slug, team);
        }
    }

    public async Task<FixIssueResult> FixIssue(IRepositoryIssue issue, RepositoryMetadata repositoryMetadata)
    {
        if (issue is MissedReviewersOnPullRequest missedReviewersOnPullRequest)
        {
            if (repositoryMetadata.Ownership != null)
            {
                if (_knownTeams.TryGetValue(repositoryMetadata.Ownership, out var team))
                {
                    await _gitHubService.AssignTeamAsReviewer(repositoryMetadata.Repository.Id,
                        missedReviewersOnPullRequest.PullRequestNumber, team.Slug);

                    return new FixIssueResult(FixStatus.Fixed, null);
                }

                return new FixIssueResult(FixStatus.NotFixed, "Team not found");
            }

            return new FixIssueResult(FixStatus.NotFixed, "Repository ownership is not known");
        }

        return new FixIssueResult(FixStatus.NotFixed, null);
    }
}
