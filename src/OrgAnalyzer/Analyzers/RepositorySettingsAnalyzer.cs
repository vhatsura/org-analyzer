namespace OrgAnalyzer.Analyzers;

public record MergeCommitAllowed : IRepositoryIssue
{
    public string Title => "Merge commit allowed";
}

public record SquashMergeDisabled : IRepositoryIssue
{
    public string Title => "Squash merge disabled";
}

public record AutoMergeDisabled : IRepositoryIssue
{
    public string Title => "Auto merge disabled";
}

public record DeleteBranchOnMergeDisabled : IRepositoryIssue
{
    public string Title => "Delete branch on merge disabled";
}

public record WikiEnabled : IRepositoryIssue
{
    public string Title => "Wiki enabled";
}

public class RepositorySettingsAnalyzer : IRepositoryAnalyzer
{
    private readonly GitHubService _gitHubService;

    public RepositorySettingsAnalyzer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public ValueTask Initialize()
    {
        return ValueTask.CompletedTask;
    }

    public async ValueTask<IReadOnlyList<IRepositoryIssue>> RunAnalysis(RepositoryMetadata repositoryMetadata)
    {
        var issues = new List<IRepositoryIssue>();

        var settings = await _gitHubService.RepositorySettings(repositoryMetadata.Repository.Owner.Login,
            repositoryMetadata.Repository.Name);

        if (settings.MergeCommitAllowed) issues.Add(new MergeCommitAllowed());
        if (!settings.SquashMergeAllowed) issues.Add(new SquashMergeDisabled());
        if (!settings.AutoMergeAllowed) issues.Add(new AutoMergeDisabled());
        if (!settings.DeleteBranchOnMerge) issues.Add(new DeleteBranchOnMergeDisabled());
        if (settings.HasWikiEnabled) issues.Add(new WikiEnabled());

        // todo: check that discussions are enabled + AllowUpdateBranch is true as well as SquashPrTitleUsedAsDefault

        return issues;
    }
}
