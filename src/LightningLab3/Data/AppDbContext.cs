using LightningLab3.Models;
using Microsoft.EntityFrameworkCore;

namespace LightningLab3.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Game> Games => Set<Game>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Game>().HasData(
            new Game { Id = 1, Title = "Hollow Knight",              Genre = "Metroidvania", Platform = "PC",             ReleaseYear = 2017 },
            new Game { Id = 2, Title = "Celeste",                    Genre = "Platformer",   Platform = "PC / Switch",    ReleaseYear = 2018 },
            new Game { Id = 3, Title = "Hades",                      Genre = "Roguelike",    Platform = "PC",             ReleaseYear = 2020 },
            new Game { Id = 4, Title = "Stardew Valley",             Genre = "Simulation",   Platform = "PC",             ReleaseYear = 2016 },
            new Game { Id = 5, Title = "Breath of the Wild",         Genre = "Adventure",    Platform = "Nintendo Switch", ReleaseYear = 2017 }
        );
    }
}
