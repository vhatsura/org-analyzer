using Octokit;

namespace OrgAnalyzer;

public static class CodeOwnershipAnalyzer
{
    internal static async Task AnalyzeCodeOwnership()
    {
        var client = GitHubApiClient.CreateRestClient();
        var gqlConnection = GitHubApiClient.CreateGraphQlConnection();
        var repositories =
            await client.Repository.GetAllForOrg(Program.Organization, new ApiOptions { PageSize = 100 });

        if (repositories.Count == 100) throw new InvalidOperationException();

        foreach (var repository in repositories)
        {
            
        }
    }
}
