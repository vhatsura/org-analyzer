using Octokit.GraphQL;

namespace OrgAnalyzer.Analyzers;

public record MissedBranchProtection : IRepositoryIssue
{
    public string Title => "Missed branch protection";
}

public record ApprovingReviewsNonRequired(ID BranchProtectionRuleId) : IRepositoryIssue
{
    public string Title => "Approving reviews non-required";
}

public record CodeOwnerReviewsNonRequired(ID BranchProtectionRuleId) : IRepositoryIssue
{
    public string Title => "Code owner reviews non-required";
}

public record ConversationResolutionNonRequired(ID BranchProtectionRuleId) : IRepositoryIssue
{
    public string Title => "Conversation resolution non-required";
}

public record InvalidApprovingReviewCount
    (ID BranchProtectionRuleId, int ActualCount, int ExpectedCount) : IRepositoryIssue
{
    public string Title => $"Invalid approving review count: {ActualCount}/{ExpectedCount}";
}

public record StatusChecksNonRequired(ID BranchProtectionRuleId) : IRepositoryIssue
{
    public string Title => "Status checks non-required";
}

public record StrictStatusChecksNonRequired(ID BranchProtectionRuleId) : IRepositoryIssue
{
    public string Title => "Strict status checks non-required";
}

public record LinearHistoryNonRequired(ID BranchProtectionRuleId) : IRepositoryIssue
{
    public string Title => "Linear history non-required";
}

public record AdminsAreNotEnforced(ID BranchProtectionRuleId) : IRepositoryIssue
{
    public string Title => "Admins are not enforced";
}

public record ForcePushesAreAllowed(ID BranchProtectionRuleId) : IRepositoryIssue
{
    public string Title => "Force pushes are allowed";
}

public record DeletionsAreAllowed(ID BranchProtectionRuleId) : IRepositoryIssue
{
    public string Title => "Deletions are allowed";
}

public record StaleReviewsAreNotDismissed(ID BranchProtectionRuleId) : IRepositoryIssue
{
    public string Title => "Stale reviews are not dismissed";
}

public class RepositoryBranchProtectionAnalyzer : IRepositoryAnalyzer
{
    private readonly GitHubService _gitHubService;

    public RepositoryBranchProtectionAnalyzer(GitHubService gitHubService)
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

        var result = await _gitHubService.BranchProtection(repositoryMetadata.Repository.Owner.Login,
            repositoryMetadata.Repository.Name, repositoryMetadata.Repository.DefaultBranch);

        if (result == null) return new[] { new MissedBranchProtection() };

        var (branchProtection, id) = result.Value;

        if (!branchProtection.EnforceAdmins.Enabled)
        {
            issues.Add(new AdminsAreNotEnforced(id));
        }

        if (branchProtection.RequiredPullRequestReviews == null)
        {
            issues.Add(new ApprovingReviewsNonRequired(id));
        }
        else
        {
            if (!branchProtection.RequiredPullRequestReviews.RequireCodeOwnerReviews)
            {
                issues.Add(new CodeOwnerReviewsNonRequired(id));
            }

            if (!branchProtection.RequiredPullRequestReviews.DismissStaleReviews)
            {
                issues.Add(new StaleReviewsAreNotDismissed(id));
            }

            if (branchProtection.RequiredPullRequestReviews.RequiredApprovingReviewCount != 1)
            {
                issues.Add(new InvalidApprovingReviewCount(id,
                    branchProtection.RequiredPullRequestReviews.RequiredApprovingReviewCount, 1));
            }
        }

        if (branchProtection.RequiredStatusChecks == null)
        {
            issues.Add(new StatusChecksNonRequired(id));
        }
        else
        {
            if (!branchProtection.RequiredStatusChecks.Strict)
            {
                issues.Add(new StrictStatusChecksNonRequired(id));
            }
        }

        // todo: waiting for the new version of Octokit with https://github.com/octokit/octokit.net/pull/2485 changes

        /*
            RequiresConversationResolution = true,
            RequiresLinearHistory = true,
            AllowsForcePushes = false,
            AllowsDeletions = false,
         */


        return issues;
    }
}
