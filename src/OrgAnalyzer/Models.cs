using Octokit;

namespace OrgAnalyzer;

public record RepositorySettings(bool MergeCommitAllowed, bool RebaseMergeAllowed, bool SquashMergeAllowed,
    bool AutoMergeAllowed, bool DeleteBranchOnMerge, bool HasWikiEnabled);

public record RepositoryMetadata(Repository Repository, string? Ownership);
