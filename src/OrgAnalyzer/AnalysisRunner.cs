using OrgAnalyzer.Analyzers;

namespace OrgAnalyzer;

public class AnalysisRunner
{
    private readonly GitHubService _gitHubService;
    private readonly IEnumerable<IRepositoryAnalyzer> _analyzers;
    private readonly IDictionary<Type, IRepositoryIssueFixer> _fixers;

    public AnalysisRunner(GitHubService gitHubService, IEnumerable<IRepositoryAnalyzer> analyzers,
        IEnumerable<IRepositoryIssueFixer> fixers)
    {
        _gitHubService = gitHubService;
        _analyzers = analyzers;

        _fixers = fixers.SelectMany(x => x.SupportedTypes.Select(t => (Type: t, Fixer: x)))
            .ToDictionary(x => x.Type, x => x.Fixer);
    }

    public async Task RunAnalyses()
    {
        foreach (var analyzer in _analyzers)
        {
            await analyzer.Initialize();
        }

        foreach (var fixer in _fixers.Values)
        {
            await fixer.Initialize();
        }

        var result = await AnalyzeRepositories();
        var fixes =
            new List<(RepositoryMetadata Metadata, List<(IRepositoryIssue Issue, bool Fixed)> Issues)>(result.Count);

        foreach (var (metadata, issues) in result)
        {
            var list = new List<(IRepositoryIssue Issue, bool Fixed)>();
            foreach (var issue in issues)
            {
                if (_fixers.TryGetValue(issue.GetType(), out var fixer))
                {
                    var fixResult = await fixer.FixIssue(issue, metadata);
                    list.Add((issue, fixResult));
                }
                else
                {
                    list.Add((issue, false));
                }
            }

            fixes.Add((metadata, list));
        }

        PrintIssues(fixes);
    }

    private async Task<List<(RepositoryMetadata RepositoryMetadata, List<IRepositoryIssue> Issues)>>
        AnalyzeRepositories()
    {
        var issues = new List<(RepositoryMetadata RepositoryMetadata, List<IRepositoryIssue> Issues)>();

        await foreach (var repositories in _gitHubService.OrganizationRepositories())
        {
            foreach (var repository in repositories)
            {
                var repositoryTopics = await _gitHubService.RepositoryTopics(repository);

                var ownershipTopic = repositoryTopics.FirstOrDefault(x => x.StartsWith("ownership-"));
                var ownership = ownershipTopic?.Substring(10);

                var repositoryMetadata = new RepositoryMetadata(repository, ownership);

                var repositoryIssues = new List<IRepositoryIssue>();
                issues.Add((repositoryMetadata, repositoryIssues));

                foreach (var analyzer in _analyzers)
                {
                    var result = await analyzer.RunAnalysis(repositoryMetadata);
                    if (result.Count > 0)
                    {
                        repositoryIssues.AddRange(result);
                    }
                }
            }
        }

        return issues;
    }

    private static void PrintIssues(
        List<(RepositoryMetadata RepositoryMetadata, List<(IRepositoryIssue Issue, bool Fixed)> Issues)> issues)
    {
        foreach (var (metadata, repoIssues) in issues)
        {
            if (repoIssues.Count > 0)
            {
                Console.WriteLine($"{metadata.Repository.FullName} has issues:");
                foreach (var (issue, @fixed) in repoIssues)
                {
                    Console.WriteLine($"* {issue.Title} {(@fixed ? "✅" : "❌")}");
                }

                Console.WriteLine("--------------------------------------------------------------------------------");
            }
        }
    }
}
