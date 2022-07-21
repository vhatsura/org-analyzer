using Octokit;
using OrgAnalyzer.Analyzers;

namespace OrgAnalyzer;

public class AnalysisRunner
{
    private readonly GitHubService _gitHubService;
    private readonly IEnumerable<IRepositoryAnalyzer> _repositoryAnalyzers;
    private readonly IEnumerable<IOrganizationAnalyzer> _organizationAnalyzers;
    private readonly IDictionary<Type, IRepositoryIssueFixer> _fixersByType;
    private readonly IReadOnlyList<IRepositoryIssueFixer> _fixers;

    public AnalysisRunner(GitHubService gitHubService, IEnumerable<IRepositoryAnalyzer> repositoryAnalyzers,
        IEnumerable<IRepositoryIssueFixer> fixers, IEnumerable<IOrganizationAnalyzer> organizationAnalyzers)
    {
        _gitHubService = gitHubService;
        _repositoryAnalyzers = repositoryAnalyzers;
        _organizationAnalyzers = organizationAnalyzers;

        _fixers = fixers.ToList();
        _fixersByType = _fixers.SelectMany(x => x.SupportedTypes.Select(t => (Type: t, Fixer: x)))
            .ToDictionary(x => x.Type, x => x.Fixer);
    }

    public async Task RunAnalyses()
    {
        foreach (var analyzer in _repositoryAnalyzers)
        {
            await analyzer.Initialize();
        }

        foreach (var analyzer in _organizationAnalyzers)
        {
            await analyzer.Initialize();
        }

        foreach (var fixer in _fixers)
        {
            await fixer.Initialize();
        }

        var organizationIssues = await AnalyzeOrganization();
        PrintOrganizationIssues(organizationIssues);

        var result = await AnalyzeRepositories();
        var fixes =
            new List<(RepositoryMetadata Metadata, List<(IRepositoryIssue Issue, bool Fixed)> Issues)>(result.Count);

        foreach (var (metadata, issues) in result)
        {
            var list = new List<(IRepositoryIssue Issue, bool Fixed)>();
            foreach (var issue in issues)
            {
                if (_fixersByType.TryGetValue(issue.GetType(), out var fixer))
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

        PrintRepositoryIssues(fixes);
    }

    private async Task<IReadOnlyList<IOrganizationIssue>> AnalyzeOrganization()
    {
        var organizationIssues = new List<IOrganizationIssue>();
        foreach (var organizationAnalyzer in _organizationAnalyzers)
        {
            var issues = await organizationAnalyzer.RunAnalysis();
            organizationIssues.AddRange(issues);
        }

        return organizationIssues;
    }

    private async Task<List<(RepositoryMetadata RepositoryMetadata, List<IRepositoryIssue> Issues)>>
        AnalyzeRepositories()
    {
        var issues = new List<(RepositoryMetadata RepositoryMetadata, List<IRepositoryIssue> Issues)>();

        await foreach (var repositories in _gitHubService.OrganizationRepositories())
        {
            foreach (var repository in repositories.Where(x => !x.Archived))
            {
                var repositoryMetadata = await RepositoryMetadata(repository);

                var repositoryIssues = new List<IRepositoryIssue>();
                issues.Add((repositoryMetadata, repositoryIssues));

                foreach (var analyzer in _repositoryAnalyzers)
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

    private async Task<RepositoryMetadata> RepositoryMetadata(Repository repository)
    {
        string? ownership = null;
        bool multipleOwnerships = false;
        RepositoryType type = RepositoryType.Unknown;
        bool multipleTypes = false;

        var repositoryTopics = await _gitHubService.RepositoryTopics(repository);

        foreach (var repositoryTopic in repositoryTopics)
        {
            if (repositoryTopic.StartsWith("ownership-"))
            {
                if (ownership != null)
                {
                    multipleOwnerships = true;
                }

                ownership = repositoryTopic[10..];
            }
            else if (repositoryTopic.StartsWith("type-"))
            {
                if (type != RepositoryType.Unknown)
                {
                    multipleTypes = true;
                }

                type = repositoryTopic[5..] switch
                {
                    "service" => RepositoryType.Service,
                    "library" => RepositoryType.Library,
                    _ => RepositoryType.Unknown,
                };
            }
        }

        if (multipleOwnerships)
        {
            ownership = null;
        }

        if (multipleTypes)
        {
            type = RepositoryType.Unknown;
        }

        return new RepositoryMetadata(repository, ownership, type);
    }

    private void PrintOrganizationIssues(IReadOnlyList<IOrganizationIssue> issues)
    {
        if (issues.Count > 0)
        {
            Console.WriteLine($"{_gitHubService.Organization} has {issues.Count} issues:");
            foreach (var organizationIssue in issues)
            {
                Console.WriteLine($"\t* {organizationIssue.Title}");
            }
        }
    }

    private static void PrintRepositoryIssues(
        List<(RepositoryMetadata RepositoryMetadata, List<(IRepositoryIssue Issue, bool Fixed)> Issues)> issues)
    {
        var totalIssuesCount = issues.Sum(x => x.Issues.Count);
        var fixedIssuesCount = issues.Sum(x => x.Issues.Count(x => x.Fixed));

        Console.WriteLine($"{totalIssuesCount} issues were found across all repositories");
        Console.WriteLine($"{fixedIssuesCount} issues were fixed");

        foreach (var (metadata, repoIssues) in issues)
        {
            if (repoIssues.Count > 0)
            {
                Console.WriteLine($"{metadata.Repository.FullName} has issues:");
                foreach (var (issue, @fixed) in repoIssues)
                {
                    Console.WriteLine($"\t* {issue.Title} - {(@fixed ? "✅" : "❌")}");
                }

                Console.WriteLine("--------------------------------------------------------------------------------");
            }
        }
    }
}
