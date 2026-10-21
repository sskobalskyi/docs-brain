using DocsBrain.Core;

namespace DocsBrain.Tests;

public class ResultTests
{
    [Fact]
    public void Success_exposes_value()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_exposes_error_and_hides_value()
    {
        Result<int> result = new Error("docs.not_found", "Document not found.");

        Assert.False(result.IsSuccess);
        Assert.Equal("docs.not_found", result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}
