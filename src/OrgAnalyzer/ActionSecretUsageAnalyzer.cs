using System.Text;
using System.Text.Json;
using Octokit;

namespace OrgAnalyzer;

public static class ActionSecretUsageAnalyzer
{
    internal static async Task AnalyzeActionSecretUsage(string secretName)
    {
        var client = GitHubApiClient.CreateRestClient();

        var repositories =
            await client.Repository.GetAllForOrg(Program.Organization, new ApiOptions { PageSize = 100 });

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
                            await client.Repository.Content.GetRawContent(Program.Organization, repository.Name, content.Path);

                        var rawContent = Encoding.UTF8.GetString(rawContentByteArray);

                        if (rawContent.Contains($"secrets.{secretName}"))
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

        var stringData = JsonSerializer.Serialize(filesContainsSecret, new JsonSerializerOptions { WriteIndented = true });
    }
}
