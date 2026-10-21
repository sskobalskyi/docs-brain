using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace DocsBrain.Api.Ai;

public sealed class AiOptions
{
    /// <summary><c>ollama</c> (local models) or <c>mock</c> (offline, deterministic).</summary>
    public string Provider { get; set; } = "ollama";
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string ChatModel { get; set; } = "llama3.2:3b";
}

public static class AiSetup
{
    // Options are read when the services are first resolved, so test and environment overrides apply.
    public static IServiceCollection AddAiProviders(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection("Ai"));

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            return options.Provider switch
            {
                "mock" => new HashingEmbeddingGenerator(),
                "ollama" => new OllamaApiClient(new Uri(options.OllamaUrl), options.EmbeddingModel),
                _ => throw new InvalidOperationException($"Unknown Ai:Provider '{options.Provider}'."),
            };
        });

        services.AddSingleton<IChatClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
            return options.Provider switch
            {
                "mock" => new ExtractiveChatClient(),
                "ollama" => new OllamaApiClient(new Uri(options.OllamaUrl), options.ChatModel),
                _ => throw new InvalidOperationException($"Unknown Ai:Provider '{options.Provider}'."),
            };
        });

        return services;
    }
}
