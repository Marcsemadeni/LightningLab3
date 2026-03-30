using LightningLab3.Models;

namespace LightningLab3.Tests;

/// <summary>
/// These tests always pass. Run them first to confirm your test runner is working.
/// </summary>
public class SanityTests
{
    [Fact]
    public void Game_Model_HasExpectedProperties()
    {
        var game = new Game
        {
            Id = 1,
            Title = "Hollow Knight",
            Genre = "Metroidvania",
            Platform = "PC",
            ReleaseYear = 2017
        };

        Assert.Equal(1, game.Id);
        Assert.Equal("Hollow Knight", game.Title);
        Assert.Equal("Metroidvania", game.Genre);
        Assert.Equal("PC", game.Platform);
        Assert.Equal(2017, game.ReleaseYear);
    }

    [Fact]
    public void Game_DefaultTitle_IsEmptyString()
    {
        var game = new Game();
        Assert.Equal(string.Empty, game.Title);
    }
}
