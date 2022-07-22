namespace OrgAnalyzer.Models;

public enum FixStatus
{
    Fixed,
    NotFixed,
    InProgress
}

public record FixIssueResult(FixStatus Status, string? Message);
