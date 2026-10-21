using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using DocsBrain.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocsBrain.Tests;

[Collection("postgres")]
public class ChatApiTests(PostgresFixture postgres)
{
    private sealed class FailingChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Ollama is down.");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw new HttpRequestException("Ollama is down.");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private WebApplicationFactory<Program> CreateFactory(
        Action<IServiceCollection>? configure = null, params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Postgres", postgres.ConnectionString);
            builder.UseSetting("Ai:Provider", "mock");
            builder.UseSetting("Search:MinSimilarity", "0.2");
            foreach (var (key, value) in settings) builder.UseSetting(key, value);
            if (configure is not null) builder.ConfigureServices(configure);
        });

    private static async Task<HttpClient> ClientFor(WebApplicationFactory<Program> factory, Guid tenantId)
    {
        var client = factory.CreateClient();
        var token = await (await client.PostAsJsonAsync("/dev/token", new { tenantId })).Content.ReadAsStringAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task Upload(HttpClient client, string name, string text)
    {
        using var content = new MultipartFormDataContent { { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(text)), "file", name } };
        (await client.PostAsync("/api/documents/upload", content)).EnsureSuccessStatusCode();
    }

    private static async Task<List<JsonElement>> Ask(HttpClient client, string question)
    {
        var response = await client.PostAsJsonAsync("/api/chat", new { question });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        return body.Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(e => JsonDocument.Parse(e["data: ".Length..]).RootElement.Clone())
            .ToList();
    }

    private static string Mode(List<JsonElement> events) => events[^1].GetProperty("mode").GetString()!;

    [Fact]
    public async Task Answer_streams_sources_tokens_and_a_done_event()
    {
        await using var factory = CreateFactory();
        var client = await ClientFor(factory, DocsBrainDbContext.AcmeTenantId);
        await Upload(client, "refund-policy.txt", "Refunds are issued within fourteen days of purchase.");

        var events = await Ask(client, "How long do refunds take?");

        Assert.Equal("sources", events[0].GetProperty("type").GetString());
        Assert.Equal("refund-policy", events[0].GetProperty("sources")[0].GetProperty("title").GetString());
        Assert.Contains(events, e => e.GetProperty("type").GetString() == "token");
        var answer = string.Concat(events.Where(e => e.GetProperty("type").GetString() == "token").Select(e => e.GetProperty("text").GetString()));
        Assert.Contains("fourteen days", answer);
        Assert.Equal("answer", Mode(events));
    }

    [Fact]
    public async Task Unrelated_question_returns_no_results_without_calling_the_model()
    {
        await using var factory = CreateFactory();
        var client = await ClientFor(factory, DocsBrainDbContext.AcmeTenantId);

        var events = await Ask(client, "zzzqqq xylophone");

        Assert.Single(events);
        Assert.Equal("no_results", Mode(events));
    }

    [Fact]
    public async Task Model_failure_falls_back_to_retrieval_only()
    {
        await using var factory = CreateFactory(s => s.Replace(ServiceDescriptor.Singleton<IChatClient>(new FailingChatClient())));
        var client = await ClientFor(factory, DocsBrainDbContext.AcmeTenantId);
        await Upload(client, "warranty.txt", "The warranty covers manufacturing defects for two years.");

        var events = await Ask(client, "What does the warranty cover?");

        Assert.Equal("sources", events[0].GetProperty("type").GetString());
        Assert.Equal("retrieval_only", Mode(events));
        Assert.Equal("llm_unavailable", events[^1].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Exhausted_token_budget_falls_back_to_retrieval_only()
    {
        await using var factory = CreateFactory(settings: ("Chat:DailyTokenBudget", "1"));
        var client = await ClientFor(factory, DocsBrainDbContext.GlobexTenantId);
        await Upload(client, "handbook.txt", "Employees receive twenty five vacation days per year.");

        Assert.Equal("answer", Mode(await Ask(client, "How many vacation days do employees receive?")));

        var second = await Ask(client, "How many vacation days do employees receive?");
        Assert.Equal("retrieval_only", Mode(second));
        Assert.Equal("budget_exceeded", second[^1].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Chat_is_rate_limited_per_tenant()
    {
        await using var factory = CreateFactory(settings: ("Chat:RequestsPerMinute", "2"));
        var acme = await ClientFor(factory, DocsBrainDbContext.AcmeTenantId);
        var globex = await ClientFor(factory, DocsBrainDbContext.GlobexTenantId);

        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.OK, (await acme.PostAsJsonAsync("/api/chat", new { question = "anything" })).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await acme.PostAsJsonAsync("/api/chat", new { question = "anything" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await globex.PostAsJsonAsync("/api/chat", new { question = "anything" })).StatusCode);
    }

    [Fact]
    public async Task Chat_requires_authentication()
    {
        await using var factory = CreateFactory();

        var response = await factory.CreateClient().PostAsJsonAsync("/api/chat", new { question = "hi" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
