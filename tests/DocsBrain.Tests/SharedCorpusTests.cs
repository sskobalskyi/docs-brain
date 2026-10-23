using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DocsBrain.Api.Data;
using DocsBrain.Core.Tenancy;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DocsBrain.Tests;

/// <summary>The shared corpus: written only by a service, searched by every tenant, never writable by a tenant.</summary>
[Collection("postgres")]
public class SharedCorpusTests(PostgresFixture postgres) : IDisposable
{
    private readonly TestIdp _idp = new();
    private WebApplicationFactory<Program>? _factory;

    private WebApplicationFactory<Program> Factory => _factory ??= _idp.CreateFactory(postgres);

    public void Dispose() => _factory?.Dispose();

    private static MultipartFormDataContent File(string text, string name = "care.md") =>
        new() { { new ByteArrayContent(Encoding.UTF8.GetBytes(text)), "file", name } };

    private HttpRequestMessage Put(string title, string text, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/shared/documents/{Uri.EscapeDataString(title)}") { Content = File(text) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? _idp.Token());
        return request;
    }

    private async Task<HttpClient> UserClient(Guid tenantId)
    {
        var client = Factory.CreateClient();
        var token = await (await client.PostAsJsonAsync("/dev/token", new { tenantId })).Content.ReadAsStringAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<List<JsonElement>> Search(HttpClient client, string query) =>
        await client.GetFromJsonAsync<List<JsonElement>>($"/api/search?q={Uri.EscapeDataString(query)}") ?? [];

    [Fact]
    public async Task Every_tenant_finds_a_shared_document_but_does_not_list_it()
    {
        var title = $"Fern care {Guid.NewGuid():N}";
        var created = await Factory.CreateClient().SendAsync(Put(title, "Ferns like humid air and indirect light. Water when the top soil feels dry."));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        foreach (var tenant in new[] { Guid.NewGuid(), DocsBrainDbContext.AcmeTenantId })
        {
            var client = await UserClient(tenant);
            var hits = await Search(client, "how much light do ferns like");
            Assert.Contains(hits, h => h.GetProperty("title").GetString() == title && h.GetProperty("shared").GetBoolean());
            Assert.DoesNotContain(
                await client.GetFromJsonAsync<List<JsonElement>>("/api/documents") ?? [],
                d => d.GetProperty("title").GetString() == title);
        }
    }

    [Fact]
    public async Task Private_documents_stay_private_next_to_the_shared_corpus()
    {
        var acme = await UserClient(DocsBrainDbContext.AcmeTenantId);
        var globex = await UserClient(DocsBrainDbContext.GlobexTenantId);
        var marker = $"zebrafinch{Guid.NewGuid():N}";
        (await acme.PostAsync("/api/documents/upload", File($"The {marker} protocol is internal to Acme.", "secret.md"))).EnsureSuccessStatusCode();

        Assert.NotEmpty(await Search(acme, marker));
        Assert.Empty(await Search(globex, marker));
    }

    [Fact]
    public async Task Uploading_a_title_again_replaces_the_previous_version()
    {
        var title = $"Basil care {Guid.NewGuid():N}";
        var client = Factory.CreateClient();
        await client.SendAsync(Put(title, "Basil likes warmth."));
        await client.SendAsync(Put(title, "Basil needs six hours of sun."));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/shared/documents");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _idp.Token());
        var documents = (await (await client.SendAsync(request)).Content.ReadFromJsonAsync<List<JsonElement>>())!
            .Where(d => d.GetProperty("title").GetString() == title).ToList();

        Assert.Single(documents);
        var hits = await Search(await UserClient(Guid.NewGuid()), "basil sun");
        Assert.Contains(hits, h => h.GetProperty("text").GetString()!.Contains("six hours of sun"));
        Assert.DoesNotContain(hits, h => h.GetProperty("text").GetString()!.Contains("likes warmth"));
    }

    [Fact]
    public async Task A_failed_replacement_keeps_the_old_version()
    {
        var title = $"Mint care {Guid.NewGuid():N}";
        var client = Factory.CreateClient();
        await client.SendAsync(Put(title, "Mint spreads fast, grow it in a pot."));

        var empty = await client.SendAsync(Put(title, "   "));

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        var hits = await Search(await UserClient(Guid.NewGuid()), "mint pot");
        Assert.Contains(hits, h => h.GetProperty("title").GetString() == title);
    }

    [Fact]
    public async Task A_service_can_delete_a_shared_document()
    {
        var title = $"Aloe care {Guid.NewGuid():N}";
        var client = Factory.CreateClient();
        var created = await (await client.SendAsync(Put(title, "Aloe stores water in its leaves."))).Content.ReadFromJsonAsync<JsonElement>();

        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/shared/documents/{created.GetProperty("id").GetGuid()}");
        delete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _idp.Token());

        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(delete)).StatusCode);
        Assert.DoesNotContain(await Search(await UserClient(Guid.NewGuid()), "aloe leaves"), h => h.GetProperty("title").GetString() == title);
    }

    [Fact]
    public async Task A_tenant_cannot_write_or_delete_shared_documents()
    {
        var user = await UserClient(DocsBrainDbContext.AcmeTenantId);

        var put = new HttpRequestMessage(HttpMethod.Put, "/api/shared/documents/Hijack") { Content = File("x y z") };
        put.Headers.Authorization = user.DefaultRequestHeaders.Authorization;
        var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/shared/documents/{Guid.NewGuid()}");
        delete.Headers.Authorization = user.DefaultRequestHeaders.Authorization;

        Assert.Equal(HttpStatusCode.Forbidden, (await user.SendAsync(put)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.SendAsync(delete)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/shared/documents")).StatusCode);
    }

    [Fact]
    public async Task A_service_token_without_the_role_cannot_write_shared_documents()
    {
        var response = await Factory.CreateClient().SendAsync(Put("Nope", "x y z", _idp.Token(roles: ["offline_access"])));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Nobody_can_act_as_the_shared_tenant()
    {
        // A tenant claim naming the shared tenant (dev token) and a header naming it (service token) are both refused.
        var asShared = await UserClient(SharedTenant.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await asShared.PostAsync("/api/documents/upload", File("x y z"))).StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/documents");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _idp.Token());
        request.Headers.Add("X-Tenant-Id", SharedTenant.Id.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await Factory.CreateClient().SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Titles_with_odd_characters_are_rejected()
    {
        var response = await Factory.CreateClient().SendAsync(Put("bad;title", "x y z"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Chat_cites_a_shared_source_and_marks_it()
    {
        var title = $"Orchid care {Guid.NewGuid():N}";
        await Factory.CreateClient().SendAsync(Put(title, "Orchids need bright indirect light and weekly watering."));
        var client = await UserClient(Guid.NewGuid());

        var response = await client.PostAsJsonAsync("/api/chat", new { question = "How often should I water my orchids?" });
        var events = (await response.Content.ReadAsStringAsync())
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(e => JsonDocument.Parse(e["data: ".Length..]).RootElement.Clone())
            .ToList();

        var sources = events.First(e => e.GetProperty("type").GetString() == "sources").GetProperty("sources");
        Assert.Contains(sources.EnumerateArray(), s => s.GetProperty("title").GetString() == title && s.GetProperty("shared").GetBoolean());
        Assert.Equal("answer", events[^1].GetProperty("mode").GetString());
    }
}
