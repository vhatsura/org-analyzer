using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Model;
using Repository = Octokit.Repository;

namespace OrgAnalyzer;

public static class BranchProtectionAnalyzer
{
    internal static async Task AnalyzeBranchProtectionRules()
    {
        var client = GitHubApiClient.CreateRestClient();
        var gqlConnection = GitHubApiClient.CreateGraphQlConnection();

        var repositories =
            await client.Repository.GetAllForOrg(Program.Organization, new ApiOptions { PageSize = 100 });

        if (repositories.Count == 100) throw new InvalidOperationException();

        foreach (var repository in repositories)
        {
            try
            {
                var branchProtection =
                    await client.Repository.Branch.GetBranchProtection(repository.Id, repository.DefaultBranch);
            }
            catch (NotFoundException ex)
            {
                await CreateBranchProtectionRule(gqlConnection, repository);
            }
        }
    }

    private static async Task CreateBranchProtectionRule(Octokit.GraphQL.Connection gqlConnection,
        Repository repository)
    {
        var mutation = new Mutation()
            .CreateBranchProtectionRule(new Arg<CreateBranchProtectionRuleInput>(new CreateBranchProtectionRuleInput
            {
                Pattern = repository.DefaultBranch,
                RepositoryId = new ID(repository.NodeId),
                AllowsDeletions = false,
                AllowsForcePushes = false,
                RequiresCodeOwnerReviews = true,
                RequiresApprovingReviews = true,
                RequiresLinearHistory = true,
                RequiresStatusChecks = true,
                RequiredApprovingReviewCount = 2,
                RequiresStrictStatusChecks = true,
                RequiresConversationResolution = true,
                DismissesStaleReviews = true
            }))
            .Select(payload => new
            {
                payload.ClientMutationId
            });
        
        var result = await gqlConnection.Run(mutation);
    }
}
