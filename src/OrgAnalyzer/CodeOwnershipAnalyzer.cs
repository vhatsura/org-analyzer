using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Core;

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

        var missedOwnershipTopics = new List<Repository>();
        foreach (var repository in repositories)
        {
            var query = new Query()
                .Repository(new Arg<string>(repository.Name), new Arg<string>(repository.Owner.Login))
                .RepositoryTopics(50)
                .Select(repoTopics => new
                {
                    repoTopics.PageInfo.HasNextPage,
                    Topics = repoTopics.Edges.Select(e => e.Node.Topic.Name).ToList()
                })
                .Compile();

            var result = await gqlConnection.Run(query);

            if (result.HasNextPage) throw new InvalidOperationException();

            var ownershipTopic = result.Topics.FirstOrDefault(x => x.StartsWith("ownership-"));
            if (ownershipTopic == null)
            {
                missedOwnershipTopics.Add(repository);
                continue;
            }

            var ownershipArea = ownershipTopic.Substring(10);
        }
    }
}
