using System.Net.Http.Headers;
using System.Text;
using ActiTimeMcp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Config sources (later wins): appsettings.json, user secrets, environment variables (ACTITIME__BASEURL etc.).
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Configuration.AddEnvironmentVariables();

// stdio transport: stdout is the protocol channel, so all logs must go to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddOptions<ActiTimeOptions>()
    .Bind(builder.Configuration.GetSection(ActiTimeOptions.Section))
    .PostConfigure(o =>
    {
        // Also accept flat env vars: ACTITIME_URL, ACTITIME_USERNAME, ACTITIME_PASSWORD.
        o.BaseUrl ??= Environment.GetEnvironmentVariable("ACTITIME_URL");
        o.Username ??= Environment.GetEnvironmentVariable("ACTITIME_USERNAME");
        o.Password ??= Environment.GetEnvironmentVariable("ACTITIME_PASSWORD");
    });

builder.Services.AddHttpClient<ActiTimeClient>((sp, http) =>
{
    var o = sp.GetRequiredService<IOptions<ActiTimeOptions>>().Value;
    o.Validate();
    var baseUrl = o.BaseUrl!.TrimEnd('/');
    if (!baseUrl.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
        baseUrl += "/api/v1";
    http.BaseAddress = new Uri(baseUrl + "/");
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
        "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{o.Username}:{o.Password}")));
    http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    http.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithPromptsFromAssembly();

await builder.Build().RunAsync();
