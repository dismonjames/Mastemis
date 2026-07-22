using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

var settings = SmokeSettings.Load();
using var source = new AuthoringClient(settings.ServerUrl);
await source.LoginAsync(settings.Username, settings.Password);

var problemId = await source.CreateDraftAsync();
await source.UpdateStatementAsync(problemId);
await source.UpdateMasAsync(problemId);
await source.UpdateReferenceSolutionAsync(problemId);

Process? worker = null;
try
{
    if (settings.StartWorker)
        worker = await source.StartWorkerAsync(settings, problemId);

    var operationId = await source.StartGenerationAsync(problemId);
    await source.WaitForGenerationAsync(problemId, operationId, settings.Timeout);
    var package = await source.ExportAsync(problemId);

    var draftVersion = await source.GetDraftVersionAsync(problemId);
    await source.ReplaceDraftAsync(problemId, draftVersion, package);

    if (settings.ImportServerUrl is not null)
    {
        using var target = new AuthoringClient(settings.ImportServerUrl);
        await target.LoginAsync(settings.Username, settings.Password);
        await target.CreateNewImportAsync(package);
    }

    using var anonymous = new HttpClient { BaseAddress = settings.ServerUrl };
    var denied = await anonymous.GetAsync($"/api/problem-studio/drafts/{problemId}/tests/1/input");
    if (denied.StatusCode is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound))
        throw new InvalidOperationException("Anonymous hidden-test access was not denied.");

    Console.WriteLine(JsonSerializer.Serialize(new { ready = true, problemId, operationId, packageBytes = package.Length }));
}
finally
{
    if (worker is { HasExited: false })
    {
        worker.Kill(entireProcessTree: true);
        await worker.WaitForExitAsync();
    }
    worker?.Dispose();
}

internal sealed record SmokeSettings(Uri ServerUrl, Uri? ImportServerUrl, string Username, string Password,
    bool StartWorker, TimeSpan Timeout)
{
    public static SmokeSettings Load()
    {
        var server = RequiredUri("MASTEMIS_SMOKE_SERVER_URL");
        var import = OptionalUri("MASTEMIS_SMOKE_IMPORT_SERVER_URL");
        var username = Required("MASTEMIS_SMOKE_USERNAME");
        var password = Required("MASTEMIS_SMOKE_PASSWORD");
        var startWorker = string.Equals(Environment.GetEnvironmentVariable("MASTEMIS_SMOKE_START_WORKER"), "true", StringComparison.OrdinalIgnoreCase);
        var seconds = int.TryParse(Environment.GetEnvironmentVariable("MASTEMIS_SMOKE_TIMEOUT_SECONDS"), out var value)
            ? Math.Clamp(value, 30, 1800) : 300;
        return new(server, import, username, password, startWorker, TimeSpan.FromSeconds(seconds));
    }

    private static string Required(string name) => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
        ? Environment.GetEnvironmentVariable(name)!
        : throw new InvalidOperationException($"Required environment variable {name} is missing.");

    private static Uri RequiredUri(string name) => Uri.TryCreate(Required(name), UriKind.Absolute, out var value)
        ? value : throw new InvalidOperationException($"Environment variable {name} is not an absolute URI.");

    private static Uri? OptionalUri(string name) => string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))
        ? null : Uri.TryCreate(Environment.GetEnvironmentVariable(name), UriKind.Absolute, out var value)
            ? value : throw new InvalidOperationException($"Environment variable {name} is not an absolute URI.");
}

internal sealed class AuthoringClient : IDisposable
{
    private readonly CookieContainer cookies = new();
    private readonly HttpClient http;

    public AuthoringClient(Uri baseAddress)
    {
        http = new(new HttpClientHandler { CookieContainer = cookies }) { BaseAddress = baseAddress };
    }

    public async Task LoginAsync(string username, string password)
    {
        await EnsureAsync(await http.PostAsJsonAsync("/api/auth/login", new { username, password, rememberMe = false }), "login");
        var token = await http.GetFromJsonAsync<JsonElement>("/api/auth/antiforgery");
        http.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }

    public async Task<Guid> CreateDraftAsync()
    {
        var response = await http.PostAsJsonAsync("/api/problem-studio/drafts", new { title = $"Smoke {Guid.NewGuid():N}", defaultLocale = "en" });
        await EnsureAsync(response, "create draft");
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public async Task UpdateStatementAsync(Guid problemId)
    {
        var content = new { title = "Smoke", markdown = "# Smoke", inputDescription = "One integer.", outputDescription = "The integer.", constraints = "1 <= n <= 1", notes = "" };
        await EnsureAsync(await http.PutAsJsonAsync($"/api/problem-studio/drafts/{problemId}/statements/en",
            new { content, expectedRevision = (int?)null }), "update statement");
    }

    public async Task UpdateMasAsync(Guid problemId) => await EnsureAsync(
        await http.PutAsJsonAsync($"/api/problem-studio/drafts/{problemId}/mas",
            new { source = "test 1 { input = int(1, 1) }", expectedRevision = 0 }), "update MAS");

    public async Task UpdateReferenceSolutionAsync(Guid problemId)
    {
        const string source = "var value = Console.ReadLine(); Console.WriteLine(value);";
        await EnsureAsync(await http.PutAsJsonAsync($"/api/problem-studio/drafts/{problemId}/reference-solution",
            new { language = "csharp", sources = new[] { new { fileName = "Program.cs", contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(source)) } } }),
            "update reference solution");
    }

    public async Task<Guid> StartGenerationAsync(Guid problemId)
    {
        var response = await http.PostAsJsonAsync($"/api/problem-studio/drafts/{problemId}/generation", new { seed = 42UL });
        await EnsureAsync(response, "start generation");
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public async Task WaitForGenerationAsync(Guid problemId, Guid operationId, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        while (true)
        {
            var value = await http.GetFromJsonAsync<JsonElement>(
                $"/api/problem-studio/drafts/{problemId}/generation/{operationId}", deadline.Token);
            var status = value.GetProperty("status");
            var terminal = status.ValueKind == JsonValueKind.Number ? status.GetInt32() : status.GetString() switch
            { "Completed" => 5, "Failed" => 6, "Cancelled" => 8, _ => -1 };
            if (terminal == 5) return;
            if (terminal is 6 or 8) throw new InvalidOperationException($"Generation ended in state {status}.");
            await Task.Delay(TimeSpan.FromSeconds(1), deadline.Token);
        }
    }

    public async Task<byte[]> ExportAsync(Guid problemId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/problem-studio/drafts/{problemId}/packages/export");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var response = await http.SendAsync(request); await EnsureAsync(response, "export package");
        var metadata = await response.Content.ReadFromJsonAsync<JsonElement>();
        return await http.GetByteArrayAsync($"/api/problem-studio/drafts/{problemId}/packages/exports/{metadata.GetProperty("exportId").GetGuid()}");
    }

    public async Task<int> GetDraftVersionAsync(Guid problemId) =>
        (await http.GetFromJsonAsync<JsonElement>($"/api/problem-studio/drafts/{problemId}")).GetProperty("version").GetInt32();

    public async Task ReplaceDraftAsync(Guid problemId, int version, byte[] package)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/problem-studio/drafts/{problemId}/packages/import?expectedVersion={version}");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N")); request.Content = new ByteArrayContent(package);
        await EnsureAsync(await http.SendAsync(request), "ReplaceDraft import");
    }

    public async Task CreateNewImportAsync(byte[] package)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/problem-studio/packages/import");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N")); request.Content = new ByteArrayContent(package);
        await EnsureAsync(await http.SendAsync(request), "CreateNew import");
    }

    public async Task<Process> StartWorkerAsync(SmokeSettings settings, Guid problemId)
    {
        var response = await http.PostAsJsonAsync("/api/admin/workers", new { name = $"smoke-{problemId:N}", capacity = 1, expiresAtUtc = DateTimeOffset.UtcNow.AddHours(1) });
        await EnsureAsync(response, "register worker"); var credential = await response.Content.ReadFromJsonAsync<JsonElement>();
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false };
        start.ArgumentList.Add("run"); start.ArgumentList.Add("--project"); start.ArgumentList.Add("src/Judge/Mastemis.Judge.csproj");
        start.ArgumentList.Add("--configuration"); start.ArgumentList.Add("Release"); start.ArgumentList.Add("--no-build");
        start.Environment["Worker__Id"] = credential.GetProperty("workerId").GetProperty("value").GetGuid().ToString("D");
        start.Environment["Worker__Secret"] = credential.GetProperty("secret").GetString();
        start.Environment["Worker__ServerUrl"] = settings.ServerUrl.ToString();
        return Process.Start(start) ?? throw new InvalidOperationException("Judge worker could not be started.");
    }

    public void Dispose() => http.Dispose();

    private static async Task EnsureAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode) return;
        var detail = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"Smoke operation '{operation}' failed with {(int)response.StatusCode}: {detail[..Math.Min(detail.Length, 512)]}");
    }
}
