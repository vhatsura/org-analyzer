using System.Text.Json;
using Octokit;

namespace OrgAnalyzer;

public class OpenPullRequestAnalyzer
{
    private readonly GitHubService _gitHubService;

    public OpenPullRequestAnalyzer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    internal async Task AnalyzeOpenPullRequests()
    {
        var repositoriesMetadata = await LoadFromGitHubApiAndSaveToDataFile();

        if (repositoriesMetadata == null) throw new InvalidOperationException();

        var repositoriesWithHighestAmountOfOpenPullRequests =
            repositoriesMetadata.Select(x => new
                    { RepositoryUrl = x.Repository.HtmlUrl, PullRequestsCount = x.PullRequests.Count })
                .OrderByDescending(x => x.PullRequestsCount).ToList();

        var pullRequestsByAuthor = repositoriesMetadata.SelectMany(x => x.PullRequests)
            .GroupBy(x => x.User.Login)
            .Select(x =>
                new
                {
                    Author = x.Key,
                    PullRequests = x.Select(p => p.HtmlUrl).ToList()
                })
            .OrderByDescending(x => x.PullRequests.Count)
            .ToList();

        var pullRequestsByReviewer = repositoriesMetadata.SelectMany(x => x.PullRequests)
            .SelectMany(x => x.RequestedReviewers.Select(r => new { Reviewer = r, x.HtmlUrl }))
            .GroupBy(x => x.Reviewer.Login)
            .Select(x =>
                new
                {
                    Reviewer = x.Key,
                    PullRequests = x.Select(p => p.HtmlUrl).ToList()
                })
            .OrderByDescending(x => x.PullRequests.Count)
            .ToList();

        var today = DateTimeOffset.UtcNow;

        var oldestPRs = repositoriesMetadata.SelectMany(x => x.PullRequests.Select(p => new
            {
                Age = today - p.CreatedAt,
                Author = p.User.Login,
                Url = p.HtmlUrl,
                p.Title
            }))
            .OrderByDescending(x => x.Age)
            .ToList();

        await File.WriteAllTextAsync("./prs-by-author.json",
            JsonSerializer.Serialize(pullRequestsByAuthor, new JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllTextAsync("./prs-by-reviewer.json",
            JsonSerializer.Serialize(pullRequestsByReviewer, new JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllTextAsync("./repos-prs-count.json",
            JsonSerializer.Serialize(repositoriesWithHighestAmountOfOpenPullRequests,
                new JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllTextAsync("./oldest-prs.json",
            JsonSerializer.Serialize(oldestPRs,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    // fix deserialization. Most of properties are null
    static async Task<IList<RepositoryMetadata>?> LoadFromDataFile()
    {
        return await JsonSerializer.DeserializeAsync<List<RepositoryMetadata>>(File.OpenRead("./data.json"),
            new JsonSerializerOptions { Converters = { new StringEnumJsonConverter<PermissionLevel>() } });
    }

    async Task<IList<RepositoryMetadata>> LoadFromGitHubApiAndSaveToDataFile()
    {
        Console.WriteLine($"{DateTime.UtcNow:u} - Load from GitHub API has started");

        var repositoriesMetadata = await LoadFromGitHubApi();

        Console.WriteLine($"{DateTime.UtcNow:u} - Load from GitHub API ended");

        await File.WriteAllTextAsync("./data.json",
            JsonSerializer.Serialize(repositoriesMetadata,
                new JsonSerializerOptions { Converters = { new StringEnumJsonConverter<PermissionLevel>() } }));

        return repositoriesMetadata;
    }

    async Task<IList<RepositoryMetadata>> LoadFromGitHubApi()
    {
        var repositoriesMetadata = new List<RepositoryMetadata>();

        await foreach (var repositories in _gitHubService.OrganizationRepositories())
        {
            foreach (var repository in repositories)
            {
                await foreach (var openPullRequests in _gitHubService.OpenRepositoryPullRequests(repository.Id))
                {
                    repositoriesMetadata.Add(new RepositoryMetadata(repository, openPullRequests));
                }
            }
        }

        return repositoriesMetadata;
    }
}
