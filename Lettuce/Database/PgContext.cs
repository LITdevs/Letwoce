using System.Drawing;
using Lettuce.Database.Models;
using Lettuce.Util.Converters;
using Microsoft.EntityFrameworkCore;

namespace Lettuce.Database;

public class PgContext(DbContextOptions<PgContext> options) : DbContext(options)
{
    public DbSet<Pawn> Pawns { get; set; }
    public DbSet<Event> Events { get; set; }
    
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