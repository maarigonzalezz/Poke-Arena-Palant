using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using PokeArena.Domain.Entities;

namespace PokeArena.Infrastructure.Persistence;

/// <summary>Mapea el esquema PostgreSQL existente y comparte una unidad de trabajo por solicitud.</summary>
public sealed class PokeArenaDbContext(DbContextOptions<PokeArenaDbContext> options) : DbContext(options)
{
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Location> Locations => Set<Location>();
    internal DbSet<AbilityRecord> Abilities => Set<AbilityRecord>();
    internal DbSet<CardCodeSequence> CardCodeSequences => Set<CardCodeSequence>();
    internal DbSet<CardVersion> CardVersions => Set<CardVersion>();
    internal DbSet<LocationVersion> LocationVersions => Set<LocationVersion>();
    internal DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();
    internal DbSet<CardSearchSession> CardSearchSessions => Set<CardSearchSession>();
    internal DbSet<CardSearchEntry> CardSearchItems => Set<CardSearchEntry>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // Los scripts de database/postgres conservan la autoridad sobre esquema, vistas y triggers.
        model.HasPostgresExtension("unaccent");
        var ability = model.Entity<AbilityRecord>();
        ability.ToTable("abilities");
        ability.HasKey(x => x.Code);
        ability.Property(x => x.Code).HasMaxLength(20);
        ability.Property(x => x.Name).HasMaxLength(100);
        ability.Property(x => x.TriggerType).HasMaxLength(50);
        ability.Property(x => x.EffectType).HasMaxLength(50);
        ability.Property(x => x.RestrictedCardType).HasMaxLength(20);
        ability.Property(x => x.Parameters).HasColumnType("jsonb");

        var card = model.Entity<Card>();
        card.ToTable("cards");
        card.HasKey(x => x.Id);
        card.Property(x => x.Id).UseIdentityAlwaysColumn();
        card.Property(x => x.Name).HasMaxLength(120);
        card.Property(x => x.Code).HasMaxLength(20);
        card.HasIndex(x => x.Code).IsUnique();
        // EF y el trigger SQL impiden modificar el código de una carta ya guardada.
        card.Property(x => x.Code).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
        card.Property(x => x.CardType).HasMaxLength(20);
        card.Property(x => x.Rarity).HasMaxLength(20);
        card.Property(x => x.AbilityCode).HasMaxLength(20);
        card.Property(x => x.ImageStatus).HasMaxLength(20);
        card.HasOne<AbilityRecord>().WithMany().HasForeignKey(x => x.AbilityCode).OnDelete(DeleteBehavior.Restrict);

        var location = model.Entity<Location>();
        location.ToTable("locations");
        location.HasKey(x => x.Id);
        location.Property(x => x.Id).UseIdentityAlwaysColumn();
        location.Property(x => x.Name).HasMaxLength(120);
        location.Property(x => x.EnergyMode).HasMaxLength(20);
        location.Property(x => x.EffectType).HasMaxLength(50);
        location.Property(x => x.ImageStatus).HasMaxLength(20);
        location.Property(x => x.EffectParameters).HasColumnType("jsonb");

        var sequence = model.Entity<CardCodeSequence>();
        sequence.ToTable("card_code_sequences");
        sequence.HasKey(x => x.CardType);
        sequence.Property(x => x.CardType).HasMaxLength(20);

        var cardVersion = model.Entity<CardVersion>();
        cardVersion.ToTable("card_versions");
        cardVersion.HasKey(x => new { x.CardId, x.Version });
        cardVersion.Property(x => x.Snapshot).HasColumnType("jsonb");
        cardVersion.HasOne<Card>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);
        var locationVersion = model.Entity<LocationVersion>();
        locationVersion.ToTable("location_versions");
        locationVersion.HasKey(x => new { x.LocationId, x.Version });
        locationVersion.Property(x => x.Snapshot).HasColumnType("jsonb");
        locationVersion.HasOne<Location>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);

        var request = model.Entity<IdempotencyRequest>();
        request.ToTable("idempotency_requests");
        request.HasKey(x => x.Id);
        request.Property(x => x.Id).UseIdentityAlwaysColumn();
        request.Property(x => x.ActorId).HasMaxLength(100);
        request.Property(x => x.Operation).HasMaxLength(100);
        request.Property(x => x.IdempotencyKey).HasMaxLength(100);
        request.Property(x => x.RequestHash).HasMaxLength(128);
        request.Property(x => x.Status).HasMaxLength(20);
        request.Property(x => x.ResponseBody).HasColumnType("jsonb");
        request.HasIndex(x => new { x.ActorId, x.Operation, x.IdempotencyKey }).IsUnique().HasDatabaseName("uq_idempotency");

        var session = model.Entity<CardSearchSession>();
        session.ToTable("card_search_sessions");
        session.HasKey(x => x.Id);
        session.Property(x => x.Id).ValueGeneratedNever();
        session.Property(x => x.ActorId).HasMaxLength(100);
        session.HasIndex(x => x.ExpiresAt).HasDatabaseName("idx_card_search_expiration");
        var entry = model.Entity<CardSearchEntry>();
        entry.ToTable("card_search_items");
        entry.HasKey(x => new { x.SessionId, x.Position });
        entry.HasIndex(x => new { x.SessionId, x.CardId }).IsUnique();
        entry.HasOne<CardSearchSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        entry.HasOne<Card>().WithMany().HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Restrict);

        // Las marcas de creación siguen generándose en PostgreSQL y no alteran los contratos HTTP.
        foreach (var type in new[] { typeof(Card), typeof(Location), typeof(CardVersion), typeof(LocationVersion),
                     typeof(IdempotencyRequest), typeof(CardSearchSession) })
            model.Entity(type).Property<DateTime>("CreatedAt").HasDefaultValueSql("NOW()");

        // Aplica snake_case sin añadir atributos de persistencia a las clases de Domain.
        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(Regex.Replace(property.Name, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant());
    }

    // ExecuteDelete elimina en servidor y PostgreSQL retira los resultados por cascada.
    public Task<int> DeleteExpiredSearchSessionsAsync(CancellationToken cancellationToken = default) =>
        CardSearchSessions.Where(x => x.ExpiresAt <= DateTime.UtcNow).ExecuteDeleteAsync(cancellationToken);
}
