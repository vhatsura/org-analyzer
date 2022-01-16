using Octokit;
using Octokit.Internal;

namespace OrgAnalyzer;

public static class GitHubApiClient
{
    private const string Token = "";

    internal static GitHubClient Create()
    {
        var credentials = new Credentials(Token);
        return new GitHubClient(new ProductHeaderValue("MyApp"), new InMemoryCredentialStore(credentials));
    }
}
