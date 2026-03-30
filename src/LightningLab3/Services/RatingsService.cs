namespace LightningLab3.Services;

public class RatingsService
{
    // TODO (Lab - Step 3): This API key is hardcoded directly in source code.
    // Anyone who clones this repo can see the key. Worse — it lives in git history forever.
    // Your job is to move this into configuration so it can be set per-environment.
    private const string ApiKey = "sk-ratings-abc123-hardcoded";

    private readonly IConfiguration _configuration;

    public RatingsService(IConfiguration configuration)
    {
        _configuration = configuration; // stored but not used for the key yet — that is the bug
    }

    /// <summary>Returns the API key currently in use.</summary>
    public string GetApiKey() => ApiKey;

    /// <summary>Returns true if the provided key matches the active key.</summary>
    public bool ValidateApiKey(string key) => key == ApiKey;

    /// <summary>
    /// Simulates fetching a community rating from an external game ratings API.
    /// In a real app this would make an HTTP request using the API key as a Bearer token.
    /// </summary>
    public Task<int> GetRatingAsync(string gameTitle)
    {
        // Fake deterministic rating — pretend this is a real HTTP call
        return Task.FromResult(gameTitle.Length % 5 + 6);
    }
}
