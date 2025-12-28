using Microsoft.EntityFrameworkCore;

namespace Elia.Shared.Models;

public class AppDbContext : DbContext
{
    public DbSet<RawData> RawData => Set<RawData>();
    public DbSet<Forecast> Forecasts => Set<Forecast>();
    public DbSet<HistoricalPoint> HistoricalPoints => Set<HistoricalPoint>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Forecast>()
            .HasOne(f => f.RawData)
            .WithMany()
            .HasForeignKey(f => f.RawDataId);

        // helpful index for revision logic later
        modelBuilder.Entity<Forecast>()
            .HasIndex(f => new { f.DatasetId, f.Region, f.ValidTime, f.VersionTime });
        modelBuilder.Entity<HistoricalPoint>()
            .HasIndex(m => new
            {
                m.DatasetId,
                m.EnergyType,
                m.Region,
                m.ValidTime,
                m.OffshoreOnshore,
                m.GridConnectionType
            });
    }
}
