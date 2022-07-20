using OrgAnalyzer.Analyzers;

namespace OrgAnalyzer.Fixers;

public class RepositorySettingsFixer : IRepositoryIssueFixer
{
    private readonly GitHubService _gitHubService;

    public IEnumerable<Type> SupportedTypes
    {
        get
        {
            yield return typeof(MergeCommitAllowed);
            yield return typeof(AutoMergeDisabled);
            yield return typeof(DeleteBranchOnMergeDisabled);
            yield return typeof(WikiEnabled);
        }
    }

    public RepositorySettingsFixer(GitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public ValueTask Initialize()
    {
        return ValueTask.CompletedTask;
    }

    public async Task<bool> FixIssue(IRepositoryIssue issue, RepositoryMetadata repositoryMetadata)
    {
        if (issue is MergeCommitAllowed)
        {
            await _gitHubService.DisableMergeCommits(repositoryMetadata.Repository.Id,
                repositoryMetadata.Repository.Name);

            return true;
        }

        if (issue is AutoMergeDisabled)
        {
            await _gitHubService.AllowAutoMerge(repositoryMetadata.Repository.Id, repositoryMetadata.Repository.Name);
            return true;
        }

        if (issue is DeleteBranchOnMergeDisabled)
        {
            await _gitHubService.EnableDeleteBranchOnMerge(repositoryMetadata.Repository.Id,
                repositoryMetadata.Repository.Name);
            return true;
        }

        if (issue is WikiEnabled)
        {
            await _gitHubService.DisableWiki(repositoryMetadata.Repository.Id, repositoryMetadata.Repository.Name);
            return true;
        }

        return false;
    }
}
