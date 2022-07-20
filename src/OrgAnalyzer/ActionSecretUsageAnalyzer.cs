using Octokit;

namespace OrgAnalyzer;

public record FileReference(string RepositoryUrl, List<string> Files);

public record SecretUsage(string SecretName, FileReference[] References);

public record ActionSecretUsageResult(SecretUsage[] Usage);

public class ActionSecretUsageAnalyzer
{
    private readonly GitHubService _gitHubService;

    public ActionSecretUsageAnalyzer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    internal async Task<ActionSecretUsageResult> RunAnalysis(string[] secretNames)
    {
        var result = secretNames.ToDictionary(secretName => secretName,
            secretName => new Dictionary<long, HashSet<RepositoryContent>>());

        var repositoriesDictionary = new Dictionary<long, Repository>();

        await foreach (var repositories in _gitHubService.OrganizationRepositories())
        {
            foreach (var repository in repositories)
            {
                repositoriesDictionary.Add(repository.Id, repository);

                try
                {
                    var contents = await _gitHubService.GetAllContents(repository.Id, ".github/workflows");
                    foreach (var content in contents)
                    {
                        if (content.Type.Value == ContentType.File)
                        {
                            var rawContent = await _gitHubService.GetRawContent(repository.Name, content.Path);

                            foreach (var secretName in secretNames)
                            {
                                if (rawContent.Contains($"secrets.{secretName}"))
                                {
                                    if (!result[secretName].ContainsKey(repository.Id))
                                    {
                                        result[secretName].Add(repository.Id, new HashSet<RepositoryContent>());
                                    }

                                    result[secretName][repository.Id].Add(content);
                                }
                            }
                        }
                    }
                }
                catch (NotFoundException)
                {
                    // ignore
                }
            }
        }

        return new ActionSecretUsageResult(result.Select(x => new SecretUsage(x.Key, x.Value.Select(u =>
                new FileReference(repositoriesDictionary[u.Key].Url, u.Value.Select(c => c.Path).ToList())).ToArray())
        ).ToArray());
    }
}
