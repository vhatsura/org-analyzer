// See https://aka.ms/new-console-template for more information

using System.Text;
using System.Text.Json;
using Octokit;
using Octokit.Helpers;

namespace OrgAnalyzer;

static class Program
{
    internal const string Organization = "";

    static async Task Main()
    {
        // await ChangeActionSecret();
        await OpenPullRequestAnalyzer.AnalyzeOpenPullRequests();
    }

    static async Task ChangeActionSecret()
    {
        var client = GitHubApiClient.Create();

        var repositories = await client.Repository.GetAllForOrg(Organization, new ApiOptions { PageSize = 100 });

        if (repositories.Count == 100) throw new InvalidOperationException();

        var result = new Dictionary<long, HashSet<RepositoryContent>>();
        var repositoriesDictionary = new Dictionary<long, Repository>();

        var repoIdsWithCreatedPRs = new HashSet<long>();

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

                        if (rawContent.Contains("secrets.DEV_KAFKA_BROKERS"))
                        {
                            if (!result.ContainsKey(repository.Id))
                            {
                                result.Add(repository.Id, new HashSet<RepositoryContent>());
                            }

                            result[repository.Id].Add(content);

                            try
                            {
                                if (!repoIdsWithCreatedPRs.Contains(repository.Id))
                                {
                                    var mainReference = await client.Git.Reference.Get(repository.Id, "heads/main");

                                    await client.Git.Reference.CreateBranch(Organization, repository.Name,
                                        "staging/kafka/update", mainReference);
                                }

                                var replacedContent = rawContent.Replace("secrets.DEV_KAFKA_BROKERS",
                                    "secrets.STAGING_KAFKA_BROKERS");

                                await client.Repository.Content.UpdateFile(Organization, repository.Name, content.Path,
                                    new UpdateFileRequest("[auto] update kafka secret name for Staging",
                                        replacedContent, content.Sha) { Branch = "staging/kafka/update" });

                                if (!repoIdsWithCreatedPRs.Contains(repository.Id))
                                {
                                    await client.Repository.PullRequest.Create(repository.Id,
                                        new NewPullRequest("Update kafka secret name for Staging",
                                            "staging/kafka/update",
                                            "main") { Body = "Automatically created pull request" });
                                    repoIdsWithCreatedPRs.Add(repository.Id);
                                }
                            }
                            catch (ApiException ex)
                            {
                            }
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
        
        var stringData =
            JsonSerializer.Serialize(filesContainsSecret, new JsonSerializerOptions { WriteIndented = true });
    }
}

internal record RepositoryMetadata(Repository Repository, IReadOnlyList<PullRequest> PullRequests);
