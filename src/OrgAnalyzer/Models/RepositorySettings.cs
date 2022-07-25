namespace OrgAnalyzer.Models;

public record RepositorySettings(bool MergeCommitAllowed, bool RebaseMergeAllowed, bool SquashMergeAllowed,
    bool AutoMergeAllowed, bool DeleteBranchOnMerge, bool HasWikiEnabled);
