using System.Text;
using System.Threading.RateLimiting;
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
using User = Octokit.User;

namespace OrgAnalyzer;

public class GitHubService
{
    private readonly GitHubClient _client;
    private readonly Connection _gqlConnection;

    private static readonly RateLimiter CreateContentRateLimiter = new FixedWindowRateLimiter(
        new FixedWindowRateLimiterOptions(1, QueueProcessingOrder.OldestFirst, 10, TimeSpan.FromSeconds(1)));

    private readonly GitHubOptions _options;

    public string Organization => _options.Organization;

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

    public async Task<IReadOnlyList<PullRequest>> OpenRepositoryPullRequests(long repositoryId)
    {
        return await _client.PullRequest.GetAllForRepository(repositoryId,
            new PullRequestRequest { State = ItemStateFilter.Open });
    }

    public async Task<(BranchProtectionSettings Settings, ID Id)?> BranchProtection(string owner, string name,
        string branchName)
    {
        try
        {
            var settings = await _client.Repository.Branch.GetBranchProtection(owner, name, branchName);

            var query = new Query()
                .Repository(name, owner)
                .Select(payload => new
                {
                    Rules = payload.BranchProtectionRules(null, null, null, null).AllPages()
                        .Select(rule => new { rule.Id, rule.Pattern }).ToList()
                });

            var result = await _gqlConnection.Run(query);

            var rule = result.Rules.SingleOrDefault(x => x.Pattern == branchName);

            if (rule == null) return null;

            return (settings, rule.Id);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public async Task CreateBranchProtectionRule(CreateBranchProtectionRuleInput input)
    {
        var mutation = new Mutation()
            .CreateBranchProtectionRule(new Arg<CreateBranchProtectionRuleInput>(input))
            .Select(payload => new { payload.ClientMutationId });

        var result = await _gqlConnection.Run(mutation);
    }

    public async Task UpdateBranchProtectionRule(UpdateBranchProtectionRuleInput input)
    {
        var mutation = new Mutation()
            .UpdateBranchProtectionRule(input)
            .Select(payload => new { payload.ClientMutationId });

        var result = await _gqlConnection.Run(mutation);
    }

    public async Task<RepositorySettings> RepositorySettings(string owner, string repository)
    {
        var query = new Query()
            .Repository(repository, owner)
            .Select(payload =>
                new
                {
                    payload.MergeCommitAllowed,
                    payload.SquashMergeAllowed,
                    payload.RebaseMergeAllowed,
                    payload.AutoMergeAllowed,
                    payload.DeleteBranchOnMerge,
                    payload.HasWikiEnabled,
                    // payload.Di
                    // todo: wait for AllowUpdateBranch in the next release
                    // todo: wait for SquashPrTitleUsedAsDefault in the next release
                });

        var result = await _gqlConnection.Run(query);

        return new RepositorySettings(result.MergeCommitAllowed, result.RebaseMergeAllowed, result.SquashMergeAllowed,
            result.AutoMergeAllowed, result.DeleteBranchOnMerge, result.HasWikiEnabled);
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

    public async Task<IReadOnlyList<User>> RepositoryCollaborators(long repositoryId)
    {
        return await _client.Repository.Collaborator.GetAll(repositoryId);
    }

    public async Task<IReadOnlyList<Repository>> TeamRepositories(int teamId)
    {
        return await _client.Organization.Team.GetAllRepositories(teamId);
    }

    public async Task AddOrUpdateTeamForRepository(int teamId, string repositoryName, Permission permission)
    {
        await _client.Organization.Team.AddRepository(teamId, _options.Organization, repositoryName,
            new RepositoryPermissionRequest(permission));
    }

    public async Task<IReadOnlyList<(User User, TeamMembershipDetails Membership)>> TeamMembers(int teamId)
    {
        var members = await _client.Organization.Team.GetAllMembers(teamId);

        var result = new List<(User User, TeamMembershipDetails Membership)>(members.Count);

        foreach (var member in members)
        {
            var membershipDetails = await _client.Organization.Team.GetMembershipDetails(teamId, member.Login);

            result.Add((member, membershipDetails));
        }

        return result;
    }

    public async Task<IReadOnlyList<User>> OrganizationOwners()
    {
        var result = new List<User>();
        var orgMembers = await _client.Organization.Member.GetAll(_options.Organization);

        foreach (var orgMember in orgMembers)
        {
            var orgMembership =
                await _client.Organization.Member.GetOrganizationMembership(_options.Organization, orgMember.Login);

            if (orgMembership.Role.Value == MembershipRole.Admin)
            {
                result.Add(orgMember);
            }
        }

        return result;
    }

    public async Task DisableMergeCommits(long repositoryId, string repositoryName)
    {
        await _client.Repository.Edit(repositoryId, new RepositoryUpdate(repositoryName) { AllowMergeCommit = false });
    }

    public async Task AllowAutoMerge(long repositoryId, string repositoryName)
    {
        await _client.Repository.Edit(repositoryId, new RepositoryUpdate(repositoryName) { AllowAutoMerge = true });
    }

    public async Task EnableDeleteBranchOnMerge(long repositoryId, string repositoryName)
    {
        await _client.Repository.Edit(repositoryId,
            new RepositoryUpdate(repositoryName) { DeleteBranchOnMerge = true });
    }

    public async Task DisableWiki(long repositoryId, string repositoryName)
    {
        await _client.Repository.Edit(repositoryId, new RepositoryUpdate(repositoryName) { HasWiki = false });
    }

    public async Task<PullRequest?> CodeOwnersPullRequest(long repositoryId)
    {
        var headBranch = "codeowners";

        try
        {
            var branch = await _client.Repository.Branch.Get(repositoryId, headBranch);

            var pullRequests = await _client.Repository.PullRequest.GetAllForRepository(repositoryId);

            return pullRequests.SingleOrDefault(x => x.Head.Ref == headBranch);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public async Task<PullRequest> CreateCodeOwners(long repositoryId, string baseBranch, string content)
    {
        var headBranch = "codeowners";

        var baseReference = await _client.Git.Reference.Get(repositoryId, $"heads/{baseBranch}");

        using var lease = await CreateContentRateLimiter.WaitAsync();
        if (lease.IsAcquired)
        {
            await _client.Git.Reference.Create(repositoryId,
                new NewReference("refs/heads/" + headBranch, baseReference.Object.Sha));

            await _client.Repository.Content.CreateFile(repositoryId, ".github/CODEOWNERS",
                new CreateFileRequest("[Automated] Add CODEOWNERS file", content) { Branch = headBranch });

            return await _client.PullRequest.Create(repositoryId,
                new NewPullRequest("Add CODEOWNERS file", headBranch, baseBranch));
        }

        throw new InvalidOperationException();
    }

    public async Task<string[]> TeamCodeOwnersPaths(string teamSlug)
    {
        var query = new Query()
            .Organization(Organization)
            .Team(teamSlug)
            .Discussions()
            .AllPages()
            .Select(x => new { x.Title, x.BodyText });

        var teamDiscussions = await _gqlConnection.Run(query);

        foreach (var discussion in teamDiscussions)
        {
            if (discussion.Title == "CODEOWNERS")
            {
                return discussion.BodyText.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        return Array.Empty<string>();
    }

    public async Task AssignTeamAsReviewer(long repositoryId, int pullRequestNumber, string teamSlug)
    {
        await _client.Repository.PullRequest.ReviewRequest.Create(repositoryId, pullRequestNumber,
            new PullRequestReviewRequest(Array.Empty<string>(), new[] { teamSlug }));
    }
}
