using Octokit;

namespace OrgAnalyzer;

public record RepositorySettings(bool MergeCommitAllowed, bool RebaseMergeAllowed, bool SquashMergeAllowed,
    bool AutoMergeAllowed, bool DeleteBranchOnMerge, bool HasWikiEnabled);

public enum RepositoryType
{
    Service,
    Library,
    Frontend,
    Documentation,
    Tool,

    Unknown = 255,
}

public record RepositoryMetadata(Repository Repository, string? Ownership, RepositoryType Type);
