using Octokit;

namespace OrgAnalyzer;

public record RepositoryMetadata(Repository Repository, string? Ownership);
