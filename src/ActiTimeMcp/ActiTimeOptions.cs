namespace ActiTimeMcp;

public sealed class ActiTimeOptions
{
    public const string Section = "ActiTime";

    /// <summary>Your actiTIME URL, e.g. https://yourcompany.actitime.com (the /api/v1 suffix is added automatically).</summary>
    public string? BaseUrl { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    /// <summary>Optional IANA/Windows time zone used to resolve "today"/"yesterday". Defaults to the machine's local zone.</summary>
    public string? TimeZone { get; set; }

    /// <summary>Optional path of the favourite-tasks file. Defaults to %APPDATA%\actitime-mcp\favorites.json.</summary>
    public string? FavoritesFile { get; set; }

    /// <summary>Optional path of the file remembering submitted entries. Defaults to %APPDATA%\actitime-mcp\recent.json.</summary>
    public string? RecentFile { get; set; }

    public void Validate()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(BaseUrl)) missing.Add("ActiTime:BaseUrl (or ACTITIME_URL)");
        if (string.IsNullOrWhiteSpace(Username)) missing.Add("ActiTime:Username (or ACTITIME_USERNAME)");
        if (string.IsNullOrWhiteSpace(Password)) missing.Add("ActiTime:Password (or ACTITIME_PASSWORD)");
        if (missing.Count > 0)
            throw new InvalidOperationException("actiTIME MCP server is not configured. Missing: " + string.Join(", ", missing));
    }
}
