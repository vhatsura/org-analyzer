using Microsoft.Extensions.Options;

namespace OrgAnalyzer.Options;

public class GitHubOptions : IValidateOptions<GitHubOptions>
{
    public string Organization { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public ValidateOptionsResult Validate(string name, GitHubOptions options)
    {
        var errors = new List<string>(2);

        if (string.IsNullOrWhiteSpace(options.Organization)) errors.Add($"{nameof(Organization)} must be specified");
        if (string.IsNullOrWhiteSpace(options.Token)) errors.Add($"{nameof(Token)} must be specified");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
