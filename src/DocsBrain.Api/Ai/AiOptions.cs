using Microsoft.Extensions.AI;
using OllamaSharp;

namespace DocsBrain.Api.Ai;

public sealed class AiOptions
{
    /// <summary><c>ollama</c> (local models) or <c>mock</c> (offline, deterministic).</summary>
    public string Provider { get; set; } = "ollama";
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
}

public static class AiSetup
{
    public static IServiceCollection AddEmbeddings(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection("Ai").Get<AiOptions>() ?? new AiOptions();
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ => options.Provider switch
        {
            "mock" => new HashingEmbeddingGenerator(),
            "ollama" => new OllamaApiClient(new Uri(options.OllamaUrl), options.EmbeddingModel),
            _ => throw new InvalidOperationException($"Unknown Ai:Provider '{options.Provider}'."),
        });
        return services;
    }
}
