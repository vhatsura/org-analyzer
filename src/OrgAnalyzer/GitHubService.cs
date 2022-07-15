using System.Text;
using Microsoft.Extensions.Options;
using Octokit;
using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Model;
using OrgAnalyzer.Options;
using Connection = Octokit.GraphQL.Connection;
using PullRequest = Octokit.PullRequest;
using Repository = Octokit.Repository;
using Team = Octokit.Team;

namespace OrgAnalyzer;

public class GitHubService
{
    private readonly GitHubClient _client;
    private readonly Connection _gqlConnection;
    private readonly GitHubOptions _options;

    public GitHubService(GitHubClient client, Connection gqlConnection, IOptions<GitHubOptions> options)
    {
        _client = client;
        _gqlConnection = gqlConnection;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<RepositoryContent>> GetAllContents(long repositoryId, string contentPath)
    {
        return await _client.Repository.Content.GetAllContents(repositoryId, contentPath);
    }

    public async Task<string> GetRawContent(string repositoryName, string contentPath)
    {
        var rawContentByteArray =
            await _client.Repository.Content.GetRawContent(_options.Organization, repositoryName, contentPath);

        var rawContent = Encoding.UTF8.GetString(rawContentByteArray);

        return rawContent;
    }

    public async IAsyncEnumerable<IReadOnlyList<Repository>> OrganizationRepositories(int pageSize = 100)
    {
        var startPage = 1;
        bool isLastPage;
        do
        {
            var repositories = await _client.Repository.GetAllForOrg(_options.Organization,
                new ApiOptions { PageSize = pageSize, StartPage = startPage++ });
            yield return repositories;
            isLastPage = repositories.Count < pageSize;
        } while (!isLastPage);
    }

    public async IAsyncEnumerable<IReadOnlyList<PullRequest>> OpenRepositoryPullRequests(long repositoryId,
        int pageSize = 100)
    {
        var startPage = 1;
        bool isLastPage;

        do
        {
            var pullRequests = await _client.PullRequest.GetAllForRepository(repositoryId,
                new PullRequestRequest { State = ItemStateFilter.Open },
                new ApiOptions { PageSize = pageSize, StartPage = startPage++ });

            yield return pullRequests;

            isLastPage = pullRequests.Count < pageSize;
        } while (!isLastPage);
    }

    public async Task<BranchProtectionSettings> BranchProtection(long repositoryId, string branchName)
    {
        return await _client.Repository.Branch.GetBranchProtection(repositoryId, branchName);
    }

    public async Task CreateBranchProtectionRule(CreateBranchProtectionRuleInput input)
    {
        var mutation = new Mutation()
            .CreateBranchProtectionRule(new Arg<CreateBranchProtectionRuleInput>(input))
            .Select(payload => new { payload.ClientMutationId });

        var result = await _gqlConnection.Run(mutation);
    }

    public async Task<List<string>> RepositoryTopics(Repository repository)
    {
        var query = new Query()
            .Repository(new Arg<string>(repository.Name), new Arg<string>(repository.Owner.Login))
            .RepositoryTopics(50)
            .Select(repoTopics => new
            {
                repoTopics.PageInfo.HasNextPage, Topics = repoTopics.Edges.Select(e => e.Node.Topic.Name).ToList()
            })
            .Compile();

        var result = await _gqlConnection.Run(query);

        if (result.HasNextPage) throw new InvalidOperationException();

        return result.Topics;
    }

    public async Task<IReadOnlyList<Team>> OrganizationTeams()
    {
        return await _client.Organization.Team.GetAll(_options.Organization);
    }

    public async Task<IReadOnlyList<Team>> RepositoryTeams(long repositoryId)
    {
        return await _client.Repository.GetAllTeams(repositoryId);
    }
}
