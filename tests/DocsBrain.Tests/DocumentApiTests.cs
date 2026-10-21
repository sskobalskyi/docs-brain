using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocsBrain.Api.Data;
using DocsBrain.Api.Documents;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocsBrain.Tests;

public class DocumentApiTests : IClassFixture<DocumentApiTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DocsBrainDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<DocsBrainDbContext>>();
                services.AddDbContext<DocsBrainDbContext>(o => o.UseInMemoryDatabase(_dbName));
            });
        }
    }

    private readonly Factory _factory;

    public DocumentApiTests(Factory factory) => _factory = factory;

    private async Task<HttpClient> ClientFor(Guid tenantId)
    {
        var client = _factory.CreateClient();
        var token = await (await client.PostAsJsonAsync("/dev/token", new { tenantId })).Content.ReadAsStringAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Requests_without_a_token_are_rejected()
    {
        var response = await _factory.CreateClient().GetAsync("/api/documents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static MultipartFormDataContent FileContent(string name, string text) =>
        new() { { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(text)), "file", name } };

    [Fact]
    public async Task Uploaded_file_is_chunked_and_visible_only_to_its_tenant()
    {
        var acme = await ClientFor(DocsBrainDbContext.AcmeTenantId);
        var globex = await ClientFor(DocsBrainDbContext.GlobexTenantId);
        var text = string.Join("\n\n", Enumerable.Range(0, 30).Select(i => $"Section {i}: " + new string('a', 200)));

        var response = await acme.PostAsync("/api/documents/upload", FileContent("handbook.txt", text));
        var doc = await response.Content.ReadFromJsonAsync<DocumentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("handbook.txt", doc!.FileName);
        var chunks = await acme.GetFromJsonAsync<List<string>>($"/api/documents/{doc.Id}/chunks");
        Assert.True(chunks!.Count > 1);
        Assert.Equal(HttpStatusCode.NotFound, (await globex.GetAsync($"/api/documents/{doc.Id}/chunks")).StatusCode);
    }

    [Theory]
    [InlineData("malware.exe", "data", "ingestion.unsupported_type")]
    [InlineData("empty.txt", "   ", "ingestion.empty")]
    public async Task Invalid_uploads_are_rejected_with_a_problem_code(string name, string text, string code)
    {
        var acme = await ClientFor(DocsBrainDbContext.AcmeTenantId);

        var response = await acme.PostAsync("/api/documents/upload", FileContent(name, text));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(code, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_tenant_cannot_read_another_tenants_document()
    {
        var acme = await ClientFor(DocsBrainDbContext.AcmeTenantId);
        var globex = await ClientFor(DocsBrainDbContext.GlobexTenantId);

        var created = await acme.PostAsJsonAsync("/api/documents", new CreateDocumentRequest("Acme secret"));
        var doc = await created.Content.ReadFromJsonAsync<DocumentResponse>();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await acme.GetAsync($"/api/documents/{doc!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await globex.GetAsync($"/api/documents/{doc.Id}")).StatusCode);
        Assert.Empty(await globex.GetFromJsonAsync<List<DocumentResponse>>("/api/documents") ?? []);
    }
}
