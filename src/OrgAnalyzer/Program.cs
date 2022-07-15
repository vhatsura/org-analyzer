// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Octokit;
using Octokit.Internal;
using OrgAnalyzer;
using OrgAnalyzer.Analyzers;
using OrgAnalyzer.Options;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<GitHubOptions>(ctx.Configuration.GetSection("GitHub"));
        services.AddSingleton(new GitHubClient(new ProductHeaderValue("MyApp"),
            new InMemoryCredentialStore(new Credentials(ctx.Configuration.GetValue<string>("GitHub:Token")))));

        services.AddSingleton(new Octokit.GraphQL.Connection(new Octokit.GraphQL.ProductHeaderValue("MyApp"),
            ctx.Configuration.GetValue<string>("GitHub:Token")));

        services.AddSingleton<GitHubService>();
    })
    .Build();

//await host.RunAsync();

var gitHubService = host.Services.GetRequiredService<GitHubService>();
var analyzers = new List<IRepositoryAnalyzer>
{
    new RepositoryOwnershipTopicAnalyzer(gitHubService), new RepositoryAccessAnalyzer(gitHubService)
};

foreach (IRepositoryAnalyzer analyzer in analyzers)
{
    await analyzer.Initialize();
}

var repositoryIssues = new List<IRepositoryIssue>();

await foreach (var repositories in gitHubService.OrganizationRepositories())
{
    foreach (var repository in repositories)
    {
        repositoryIssues.Clear();

        var repositoryTopics = await gitHubService.RepositoryTopics(repository);

        var ownershipTopic = repositoryTopics.FirstOrDefault(x => x.StartsWith("ownership-"));
        var ownership = ownershipTopic?.Substring(10);

        foreach (var analyzer in analyzers)
        {
            var result = await analyzer.RunAnalysis(new RepositoryMetadata(repository, ownership));
            if (result != null)
            {
                repositoryIssues.Add(result);
            }
        }

        if (repositoryIssues.Count > 0)
        {
            Console.WriteLine($"{repository.FullName} has issues:");
            foreach (var issue in repositoryIssues)
            {
                Console.WriteLine($"* {issue.Title}");
            }

            Console.WriteLine("--------------------------------------------------------------------------------");
        }
    }
}

// var analyzer = new BranchProtectionRepositoryAnalyzer(gitHubService);

// await ChangeActionSecret();
// await OpenPullRequestAnalyzer.AnalyzeOpenPullRequests();
// await BranchProtectionAnalyzer.AnalyzeBranchProtectionRules();
//var result = await analyzer.RunAnalysis();
// await ActionSecretUsageAnalyzer.AnalyzeActionSecretUsage(new[] { "PRODUCTION_EUC1_0", "PRODUCTION_EUC1_1" });
// await ChangeActionSecret("\"${{ secrets.PRODUCTION_EUC1_0 }},${{ secrets.PRODUCTION_EUC1_1 }}\"",
//     "PRODUCTION_API_HOSTS");

// var stringData = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
// Console.WriteLine(stringData);
