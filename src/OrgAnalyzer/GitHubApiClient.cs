using Octokit;
using Octokit.Internal;

namespace OrgAnalyzer;

public static class GitHubApiClient
{
    private const string Token = "";

    internal static GitHubClient CreateRestClient()
    {
        var credentials = new Credentials(Token);
        return new GitHubClient(new ProductHeaderValue("MyApp"), new InMemoryCredentialStore(credentials));
    }

    internal static Octokit.GraphQL.Connection CreateGraphQlConnection()
    {
        return new Octokit.GraphQL.Connection(new Octokit.GraphQL.ProductHeaderValue("MyApp"), Token);
    }
}
