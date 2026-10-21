using System.Text;
using Microsoft.Extensions.AI;

namespace DocsBrain.Api.Ai;

/// <summary>
/// Deterministic offline embedding provider: hashed bag of words, L2-normalised.
/// It captures word overlap only, which is enough for tests, CI evals and running without a model.
/// </summary>
public sealed class HashingEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public const int Dimensions = 768;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = new GeneratedEmbeddings<Embedding<float>>(values.Select(v => new Embedding<float>(Embed(v))));
        return Task.FromResult(result);
    }

    public static float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        foreach (var token in Tokenizer.Tokens(text))
        {
            var hash = Fnv1a(token);
            vector[(int)(hash % Dimensions)] += (hash & 0x80000000) == 0 ? 1f : -1f;
        }

        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        if (norm > 0)
            for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
        return vector;
    }

    private static uint Fnv1a(string token)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(token))
            hash = (hash ^ b) * 16777619u;
        return hash;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}

public static class Tokenizer
{
    /// <summary>Lower-case word tokens (letters and digits, any script).</summary>
    public static IEnumerable<string> Tokens(string text)
    {
        var current = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(char.ToLowerInvariant(ch));
            }
            else if (current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }
        }

        if (current.Length > 0) yield return current.ToString();
    }
}
