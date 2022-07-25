using Octokit;

namespace OrgAnalyzer.Models;

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
