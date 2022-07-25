namespace OrgAnalyzer.Analyzers;

public record MissedReviewersOnPullRequest(string PullRequestUrl, int PullRequestNumber) : IRepositoryIssue
{
    public string Title => $"Missed reviewers on pull request {PullRequestUrl}";
}

public record PullRequestStalled(string PullRequestUrl, TimeSpan Lifetime, string UserLogin,
    IReadOnlyList<string> RequestedReviewers) : IRepositoryIssue
{
    public string Title => $"Pull request {PullRequestUrl} is stalled for ~{Lifetime.Days} days from {UserLogin}";
}

public class RepositoryPullRequestsAnalyzer : IRepositoryAnalyzer
{
    private readonly GitHubService _gitHubService;

    public RepositoryPullRequestsAnalyzer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public ValueTask Initialize()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask<IReadOnlyList<IRepositoryIssue>> RunAnalysis(RepositoryMetadata repositoryMetadata)
    {
        var issues = new List<IRepositoryIssue>();
        var now = DateTimeOffset.UtcNow;

        var repositoryPullRequests = await _gitHubService.OpenRepositoryPullRequests(repositoryMetadata.Repository.Id);

        foreach (var pullRequest in repositoryPullRequests)
        {
            if (pullRequest.RequestedReviewers.Count == 0 && pullRequest.RequestedTeams.Count == 0 &&
                !pullRequest.Draft)
            {
                issues.Add(new MissedReviewersOnPullRequest(pullRequest.HtmlUrl, pullRequest.Number));
            }

            var lifetime = now - pullRequest.CreatedAt;
            if (lifetime.TotalDays > 7)
            {
                issues.Add(new PullRequestStalled(pullRequest.HtmlUrl, lifetime, pullRequest.User.Login,
                    pullRequest.RequestedReviewers.Select(x => x.Login)
                        .Union(pullRequest.RequestedTeams.Select(x => $"@{_gitHubService.Organization}/{x.Slug}"))
                        .ToArray()));
            }
        }

        return issues;
    }
}
