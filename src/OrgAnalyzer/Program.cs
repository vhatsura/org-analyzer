// See https://aka.ms/new-console-template for more information

using System.Text;
using System.Text.Json;
using Octokit;
using Octokit.Internal;

namespace OrgAnalyzer;

static class Program
{
    private const string Token = "";
    private const string Organization = "oveo-io";

    static async Task Main()
    {
        await AnalyzeActionsSecretUsage();
    }

    static async Task AnalyzeActionsSecretUsage()
    {
        var client = CreateClient();

        var repositories = await client.Repository.GetAllForOrg(Organization, new ApiOptions { PageSize = 100 });

        if (repositories.Count == 100) throw new InvalidOperationException();

        var result = new Dictionary<long, HashSet<RepositoryContent>>();
        var repositoriesDictionary = new Dictionary<long, Repository>();

        foreach (var repository in repositories)
        {
            repositoriesDictionary.Add(repository.Id, repository);


            try
            {
                var contents = await client.Repository.Content.GetAllContents(repository.Id, ".github/workflows");
                foreach (var content in contents)
                {
                    if (content.Type.Value == ContentType.File)
                    {
                        var rawContentByteArray =
                            await client.Repository.Content.GetRawContent(Organization, repository.Name, content.Path);

                        var rawContent = Encoding.UTF8.GetString(rawContentByteArray);

                        if (rawContent.Contains("DEV_KAFKA_BROKERS"))
                        {
                            if (!result.ContainsKey(repository.Id))
                            {
                                result.Add(repository.Id, new HashSet<RepositoryContent>());
                            }

                            result[repository.Id].Add(content);
                        }
                    }
                }
            }
            catch (NotFoundException ex)
            {
                // ignore
            }
        }

        var filesContainsSecret = result.Select(x => new
        {
            RepositoryUrl = repositoriesDictionary[x.Key].Url,
            Files = x.Value.Select(c => c.Path).ToList()
        }).ToList();

        await using var memoryStream = new MemoryStream();

        var stringData = JsonSerializer.Serialize(filesContainsSecret, new JsonSerializerOptions { WriteIndented = true });
    }

    static async Task AnalyzeOpenPullRequests()
    {
        var repositoriesMetadata = await LoadFromGitHubApiAndSaveToDataFile();

        if (repositoriesMetadata == null) throw new InvalidOperationException();

        var repositoriesWithHighestAmountOfOpenPullRequests =
            repositoriesMetadata.OrderByDescending(x => x.PullRequests.Count).Take(5).ToList();
    }

    // fix deserialization. Most of properties are null
    static async Task<IList<RepositoryMetadata>?> LoadFromDataFile()
    {
        return await JsonSerializer.DeserializeAsync<List<RepositoryMetadata>>(File.OpenRead("./data.json"),
            new JsonSerializerOptions { Converters = { new StringEnumJsonConverter<PermissionLevel>() } });
    }

    static async Task<IList<RepositoryMetadata>> LoadFromGitHubApiAndSaveToDataFile()
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
        var github = CreateClient();

        var repositories = await github.Repository.GetAllForOrg(Organization, new ApiOptions { PageSize = 100 });

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

    static GitHubClient CreateClient()
    {
        var credentials = new Credentials(Token);
        return new GitHubClient(new ProductHeaderValue("MyApp"), new InMemoryCredentialStore(credentials));
    }
}

internal record RepositoryMetadata(Repository Repository, IReadOnlyList<PullRequest> PullRequests);
