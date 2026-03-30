using LightningLab3.Services;
using Microsoft.Extensions.Configuration;

namespace LightningLab3.Tests;

/// <summary>
/// Challenge tests — key rotation.
///
/// These tests demonstrate WHY configuration-based secrets are better:
/// when a key is compromised you can rotate (replace) it by only changing
/// a config value, with zero code changes and zero redeployment of new code.
///
/// EXPECTED STATE before completing the challenge: tests FAIL.
/// EXPECTED STATE after  completing the challenge: tests PASS.
/// </summary>
public class KeyRotationTests
{
    private static IConfiguration CreateConfig(string apiKey) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RatingsApi:ApiKey"] = apiKey
            })
            .Build();

    [Fact]
    public void RotatingApiKey_OldKeyIsRejected_NewKeyIsAccepted()
    {
        // Simulate the state BEFORE rotation
        var oldKey = "sk-old-key-111aaa";
        var serviceBeforeRotation = new RatingsService(CreateConfig(oldKey));
        Assert.True(serviceBeforeRotation.ValidateApiKey(oldKey), "Old key should be valid before rotation.");

        // Simulate rotation: update config to the new key (no code change required)
        var newKey = "sk-rotated-key-999zzz";
        var serviceAfterRotation = new RatingsService(CreateConfig(newKey));

        // New key must be accepted
        Assert.True(
            serviceAfterRotation.ValidateApiKey(newKey),
            "New key should be accepted after rotation."
        );

        // Old key must be rejected
        Assert.False(
            serviceAfterRotation.ValidateApiKey(oldKey),
            "Old (compromised) key must be rejected after rotation. " +
            "If this fails, the service is still using a hardcoded key — see the Challenge section."
        );
    }

    [Fact]
    public void TwoEnvironments_CanHaveDifferentKeys()
    {
        // Dev and production often use different keys so a dev key leak
        // does not compromise the production environment.
        var devKey  = "sk-dev-local-testing-abc";
        var prodKey = "sk-prod-real-secret-xyz";

        var devService  = new RatingsService(CreateConfig(devKey));
        var prodService = new RatingsService(CreateConfig(prodKey));

        Assert.True(devService.ValidateApiKey(devKey),    "Dev key should work in dev service.");
        Assert.True(prodService.ValidateApiKey(prodKey),  "Prod key should work in prod service.");
        Assert.False(devService.ValidateApiKey(prodKey),  "Prod key must NOT work in dev service.");
        Assert.False(prodService.ValidateApiKey(devKey),  "Dev key must NOT work in prod service.");
    }
}
