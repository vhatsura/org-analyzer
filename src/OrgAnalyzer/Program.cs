// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using Octokit;
using Octokit.Internal;

namespace OrgAnalyzer;

static class Program
{
    static async Task Main()
    {
        var repositoriesMetadata = await LoadFromDataFile();
        
    }


    static async Task<IList<RepositoryMetadata>?> LoadFromDataFile()
    {
        return await JsonSerializer.DeserializeAsync<List<RepositoryMetadata>>(File.OpenRead("./data.json"),
            new JsonSerializerOptions { Converters = { new StringEnumJsonConverter<PermissionLevel>() } });
    }

    static async Task<IList<RepositoryMetadata>> LoadFromGitHubApiAndSavetoDataFile()
    {
        Console.WriteLine($"{DateTime.UtcNow:u} - Load from GitHub API has started");

        var repositoriesMetadata = await LoadFromGitHubApi();

        Console.WriteLine($"{DateTime.UtcNow:u} - Load from GitHub API ended");

        await File.WriteAllTextAsync("./data.json",
            JsonSerializer.Serialize(repositoriesMetadata,
                new JsonSerializerOptions { Converters = { new StringEnumJsonConverter<PermissionLevel>() } }));

        return repositoriesMetadata;
    }
    
    static async Task<IList<RepositoryMetadata>> LoadFromGitHubApi()
    {
        var token = "";
        var organization = "oveo-io";

        var credentials = new Credentials(token);
        var github = new GitHubClient(new ProductHeaderValue("MyApp"), new InMemoryCredentialStore(credentials));

        var repositories = await github.Repository.GetAllForOrg(organization, new ApiOptions { PageSize = 100 });

        if (repositories.Count == 100) throw new InvalidOperationException();

        var repositoriesMetadata = new List<RepositoryMetadata>();

        foreach (var repository in repositories)
        {
            var openPullRequests = await github.PullRequest.GetAllForRepository(repository.Id,
                new PullRequestRequest { State = ItemStateFilter.Open },
                new ApiOptions { PageSize = 100 });

            if (openPullRequests.Count == 100) throw new InvalidOperationException();

            repositoriesMetadata.Add(new RepositoryMetadata(repository, openPullRequests));
        }

        return repositoriesMetadata;
    }
}

internal record RepositoryMetadata(Repository Repository, IReadOnlyList<PullRequest> PullRequests);
