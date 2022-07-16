// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Octokit;
using Octokit.Internal;
using OrgAnalyzer;
using OrgAnalyzer.Analyzers;
using OrgAnalyzer.Fixers;
using OrgAnalyzer.Options;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.Configure<GitHubOptions>(ctx.Configuration.GetSection("GitHub"));
        services.AddSingleton(new GitHubClient(new ProductHeaderValue("MyApp"),
            new InMemoryCredentialStore(new Credentials(ctx.Configuration.GetValue<string>("GitHub:Token")))));

        services.AddSingleton(new Octokit.GraphQL.Connection(new Octokit.GraphQL.ProductHeaderValue("MyApp"),
            ctx.Configuration.GetValue<string>("GitHub:Token")));

        services.AddSingleton<IOrganizationAnalyzer, TeamMaintainerAnalyzer>();

        services.AddSingleton<IRepositoryAnalyzer, RepositoryOwnershipTopicAnalyzer>();
        services.AddSingleton<IRepositoryAnalyzer, RepositoryAccessAnalyzer>();

        services.AddSingleton<IRepositoryIssueFixer, RepositoryAccessFixer>();

        services.AddSingleton<GitHubService>();
        services.AddSingleton<AnalysisRunner>();
    })
    .Build();


var runner = host.Services.GetRequiredService<AnalysisRunner>();
await runner.RunAnalyses();
