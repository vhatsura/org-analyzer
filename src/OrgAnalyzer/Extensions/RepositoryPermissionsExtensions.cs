using Octokit;

namespace OrgAnalyzer.Extensions;

public static class RepositoryPermissionsExtensions
{
    public static Permission ToPermission(this RepositoryPermissions permissions)
    {
        if (permissions.Admin) return Permission.Admin;
        if (permissions.Maintain) return Permission.Maintain;
        if (permissions.Push) return Permission.Push;
        if (permissions.Triage) return Permission.Triage;
        if (permissions.Pull) return Permission.Pull;

        throw new InvalidOperationException();
    }
}
