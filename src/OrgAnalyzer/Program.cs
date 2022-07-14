// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Octokit;
using Octokit.Internal;
using OrgAnalyzer;
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

var analyzer = new BranchProtectionAnalyzer(gitHubService);

// await ChangeActionSecret();
// await OpenPullRequestAnalyzer.AnalyzeOpenPullRequests();
// await BranchProtectionAnalyzer.AnalyzeBranchProtectionRules();
var result = await analyzer.RunAnalysis();
// await ActionSecretUsageAnalyzer.AnalyzeActionSecretUsage(new[] { "PRODUCTION_EUC1_0", "PRODUCTION_EUC1_1" });
// await ChangeActionSecret("\"${{ secrets.PRODUCTION_EUC1_0 }},${{ secrets.PRODUCTION_EUC1_1 }}\"",
//     "PRODUCTION_API_HOSTS");

var stringData = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(stringData);

namespace OrgAnalyzer
{
    internal record RepositoryMetadata(Repository Repository, IReadOnlyList<PullRequest> PullRequests);
}
