using Octokit.GraphQL;
using Octokit.GraphQL.Model;
using OrgAnalyzer.Analyzers;

namespace OrgAnalyzer.Fixers;

public class RepositoryBranchProtectionFixer : IRepositoryIssueFixer
{
    private readonly GitHubService _gitHubService;

    public IEnumerable<Type> SupportedTypes
    {
        get
        {
            yield return typeof(MissedBranchProtection);
        }
    }

    public RepositoryBranchProtectionFixer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public ValueTask Initialize()
    {
        return ValueTask.CompletedTask;
    }

    public async Task<bool> FixIssue(IRepositoryIssue issue, RepositoryMetadata repositoryMetadata)
    {
        if (issue is MissedBranchProtection)
        {
            await _gitHubService.CreateBranchProtectionRule(new CreateBranchProtectionRuleInput
            {
                RepositoryId = new ID(repositoryMetadata.Repository.NodeId),
                Pattern = repositoryMetadata.Repository.DefaultBranch,
                RequiresApprovingReviews = true,
                RequiresCodeOwnerReviews = true,
                RequiresConversationResolution = true,
                RequiredApprovingReviewCount = 1,
                DismissesStaleReviews = true,
                RequiresStatusChecks = true,
                RequiresStrictStatusChecks = true,
                RequiresLinearHistory = true,
                IsAdminEnforced = true,
                AllowsForcePushes = false,
                AllowsDeletions = false,
            });

            return true;
        }

        if (issue is ApprovingReviewsNonRequired approvingReviewsNonRequired)
        {
            await _gitHubService.UpdateBranchProtectionRule(new UpdateBranchProtectionRuleInput
            {
                BranchProtectionRuleId = approvingReviewsNonRequired.BranchProtectionRuleId,
                RequiresCodeOwnerReviews = true,
                DismissesStaleReviews = true,
                RequiresApprovingReviews = true,
                RequiredApprovingReviewCount = 1
            });

            return true;
        }

        if (issue is CodeOwnerReviewsNonRequired codeOwnerReviewsNonRequired)
        {
            await _gitHubService.UpdateBranchProtectionRule(new UpdateBranchProtectionRuleInput
            {
                BranchProtectionRuleId = codeOwnerReviewsNonRequired.BranchProtectionRuleId,
                RequiresCodeOwnerReviews = true,
            });

            return true;
        }

        return false;
    }
}
