using Octokit;
using Repository = Octokit.Repository;

namespace OrgAnalyzer;

public record BranchProtectionAnalyzerResult(
    HashSet<(Repository Repository, BranchProtectionSettings? BranchProtection)> MissedBranchProtectionRules) :
    IAnalyzerResult;

public class BranchProtectionAnalyzer : IAnalyzer<BranchProtectionAnalyzerResult>
{
    private readonly GitHubService _gitHubService;

    public BranchProtectionAnalyzer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public async Task<BranchProtectionAnalyzerResult> RunAnalysis()
    {
        var branchProtectionRules = new HashSet<(Repository Repository, BranchProtectionSettings? BranchProtection)>();

        await foreach (var repositories in _gitHubService.OrganizationRepositories())
        {
            foreach (var repository in repositories)
            {
                try
                {
                    var branchProtection =
                        await _gitHubService.BranchProtection(repository.Id, repository.DefaultBranch);

                    branchProtectionRules.Add((repository, branchProtection));
                }
                catch (NotFoundException ex)
                {
                    branchProtectionRules.Add((repository, null));
                    // await _gitHubService.CreateBranchProtectionRule(new CreateBranchProtectionRuleInput
                    // {
                    //     Pattern = repository.DefaultBranch,
                    //     RepositoryId = new ID(repository.NodeId),
                    //     AllowsDeletions = false,
                    //     AllowsForcePushes = false,
                    //     RequiresCodeOwnerReviews = true,
                    //     RequiresApprovingReviews = true,
                    //     RequiresLinearHistory = true,
                    //     RequiresStatusChecks = true,
                    //     RequiredApprovingReviewCount = 1,
                    //     RequiresStrictStatusChecks = true,
                    //     RequiresConversationResolution = true,
                    //     DismissesStaleReviews = true
                    // });
                }
            }
        }

        return new BranchProtectionAnalyzerResult(branchProtectionRules);
    }
}
