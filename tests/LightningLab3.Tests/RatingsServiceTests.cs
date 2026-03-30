using LightningLab3.Services;
using Microsoft.Extensions.Configuration;

namespace LightningLab3.Tests;

/// <summary>
/// These tests verify that RatingsService reads its API key from IConfiguration
/// instead of having it hardcoded.
///
/// EXPECTED STATE before Step 3: both tests FAIL.
/// EXPECTED STATE after  Step 3: both tests PASS.
/// </summary>
public class RatingsServiceTests
{
    private static IConfiguration CreateConfig(string apiKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RatingsApi:ApiKey"] = apiKey
            })
            .Build();

    [Fact]
    public void RatingsService_ValidateApiKey_UsesKeyFromConfiguration()
    {
        // Arrange: build a config with a known test key
        var config = CreateConfig("sk-test-key-from-config");
        var service = new RatingsService(config);

        // Act & Assert: the configured key should be accepted
        // This FAILS if the service ignores config and returns the hardcoded key
        Assert.True(
            service.ValidateApiKey("sk-test-key-from-config"),
            "ValidateApiKey should return true for the key stored in IConfiguration. " +
            "If this fails, the service is still using the hardcoded key — see Step 3."
        );
    }

    [Fact]
    public void RatingsService_ValidateApiKey_RejectsHardcodedKey()
    {
        // Arrange: configure a key that is different from the hardcoded one
        var config = CreateConfig("sk-test-key-from-config");
        var service = new RatingsService(config);

        // Act & Assert: the old hardcoded key must NOT be accepted
        // This FAILS if the service still uses the hardcoded constant
        Assert.False(
            service.ValidateApiKey("sk-ratings-abc123-hardcoded"),
            "ValidateApiKey should reject the old hardcoded key once configuration is used. " +
            "If this fails, the service is still using the hardcoded key — see Step 3."
        );
    }

    [Fact]
    public async Task RatingsService_GetRating_ReturnsBetween6And10()
    {
        // This test always passes — it just confirms the rating logic works
        var config = CreateConfig("sk-any-key");
        var service = new RatingsService(config);

        var rating = await service.GetRatingAsync("Hollow Knight");

        Assert.InRange(rating, 6, 10);
    }
}
