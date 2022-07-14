using CodeOwners;
using Octokit;

namespace OrgAnalyzer;

public record CodeOwnershipResult(HashSet<string> RepositoriesWithMissedOwnershipTopic);

public class CodeOwnershipAnalyzer
{
    private readonly GitHubService _gitHubService;

    public CodeOwnershipAnalyzer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    internal async Task<CodeOwnershipResult> AnalyzeCodeOwnership()
    {
        var missedOwnershipTopics = new List<Repository>();

        await foreach (var repositories in _gitHubService.OrganizationRepositories())
        {
            foreach (var repository in repositories.Where(x => !x.Archived))
            {
                var repositoryTopics = await _gitHubService.RepositoryTopics(repository);

                var ownershipTopic = repositoryTopics.FirstOrDefault(x => x.StartsWith("ownership-"));
                if (ownershipTopic == null)
                {
                    missedOwnershipTopics.Add(repository);
                    continue;
                }

                var ownershipArea = ownershipTopic.Substring(10);

                CodeOwnersParser.Parse("");
            }
        }

        return new CodeOwnershipResult(missedOwnershipTopics.Select(x => x.FullName).ToHashSet());
    }
}
