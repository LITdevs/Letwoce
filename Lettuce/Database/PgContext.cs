using System.Drawing;
using AppAny.Quartz.EntityFrameworkCore.Migrations;
using AppAny.Quartz.EntityFrameworkCore.Migrations.PostgreSQL;
using Lettuce.Database.Models;
using Lettuce.Util.Converters;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Lettuce.Database;

public class PgContext(DbContextOptions<PgContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Pawn> Pawns { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<Vote> Votes { get; set; }
    public DbSet<PlayerCountLog> PlayerCountLogs { get; set; }
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddQuartz(b => b.UsePostgreSql());

        modelBuilder.Entity<Pawn>(b =>
        {
            b.HasData(new Pawn
            {
                Id = Guid.AllBitsSet,
                DiscordId = "1334788940082970654",
                DisplayName = "Supreme Court of Lettuce",
                X = -8,
                Y = 11,
                Health = int.MaxValue,
                Actions = int.MaxValue,
                Color = Color.FromArgb(0, 146, 255, 119),
                KilledAt = null,
                KilledBy = null,
                AvatarUri = "https://015-cdn.b-cdn.net/db49f42ed7b4a0f5a209dc00f8d780d5.png",
            });
        });
        base.OnModelCreating(modelBuilder);
    }
    
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // This is the stupidest thing ever, you can only put UTC time into "timestamp with time zone"
        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetConverter>();
        
        
        configurationBuilder
            .Properties<Color>()
            .HaveConversion<ColorValueConverter>();
    }

}