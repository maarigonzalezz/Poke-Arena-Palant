using System.Text.Json;
using PokeArena.Application.Cards;
using PokeArena.Application.Common;
using PokeArena.Application.Locations;
using PokeArena.Domain.Catalogs;

namespace PokeArena.Tests;

/// <summary>Comprueba que Application rechaza datos inválidos antes de persistir o crear imágenes.</summary>
public sealed class CatalogServicesTests
{
    private static CardCreateQuery Card(string type = "Fuego") => new()
    { Name = "Carta", Cost = 2, Power = 4, Type = type, Rarity = "Común", AbilityCode = "HAB-000" };

    private static CreateLocationRequest Location(string type) => new()
    {
        Name = "Arena", Description = "Escenario", MaxCards = 4, EnergyMode = "Fixed", ExtraEnergy = 1,
        EffectType = "TypeBonus", EffectParameters = JsonSerializer.Serialize(new { type, power = 2 })
    };

    [Theory]
    [InlineData("Fuego", "FUE")] [InlineData("Agua", "AGU")] [InlineData("Planta", "PLA")]
    [InlineData("Eléctrico", "ELE")] [InlineData("Psíquico", "PSI")] [InlineData("Lucha", "LUC")]
    public void Shared_types_are_accepted_by_all_validators(string type, string prefix)
    {
        Assert.Equal(prefix, CardTypes.GetPrefix(type));
        Assert.Empty(CardCreateValidator.Validate(Card(type)));
        Assert.Empty(CardSearchValidator.Validate(new CardSearchQuery { Type = type }));
        Assert.Empty(LocationValidator.Validate(Location(type)));
    }

    [Fact]
    public async Task Invalid_search_never_reaches_repository()
    {
        var service = new CardService(new(), new UnexpectedSearch(), new ProbeTransaction(), new UnexpectedImages());
        var error = await Assert.ThrowsAsync<CatalogValidationException>(() =>
            service.SearchAsync("admin", new CardSearchQuery { MinCost = 5, MaxCost = 2 }, default));
        Assert.Equal("invalid_search_criteria", error.Type);
        Assert.Contains("costRange", error.Errors.Keys);
    }

    [Theory]
    [InlineData("Otro", "key", "type")]
    [InlineData("Fuego", "", "idempotencyKey")]
    public async Task Invalid_card_never_opens_transaction(string type, string key, string field)
    {
        var transaction = new ProbeTransaction();
        var service = new CardService(new(), new UnexpectedSearch(), transaction, new UnexpectedImages());
        var error = await Assert.ThrowsAsync<CatalogValidationException>(() =>
            service.CreateAsync("admin", Card(type), new ImageUpload([]), key, default));
        Assert.Contains(field, error.Errors.Keys);
        Assert.Equal(0, transaction.Calls);
    }

    [Fact]
    public async Task Invalid_location_never_opens_transaction()
    {
        var transaction = new ProbeTransaction();
        var service = new LocationService(new(), transaction, new UnexpectedImages());
        var error = await Assert.ThrowsAsync<CatalogValidationException>(() =>
            service.CreateAsync("admin", Location("Otro"), new ImageUpload([]), "key", default));
        Assert.Contains("location", error.Errors.Keys);
        Assert.Equal(0, transaction.Calls);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Missing_or_incompatible_ability_never_creates_images(bool exists)
    {
        var ability = exists ? new AbilityItem("HAB-000", "Agua", null, null,
            JsonSerializer.SerializeToElement(new { }), "Agua") : null;
        var transaction = new ProbeTransaction(new AbilitySession(ability));
        var service = new CardService(new(), new UnexpectedSearch(), transaction, new UnexpectedImages());
        var error = await Assert.ThrowsAsync<CatalogValidationException>(() =>
            service.CreateAsync("admin", Card(), new ImageUpload([]), "key", default));
        Assert.Contains("abilityCode", error.Errors.Keys);
        Assert.Equal(1, transaction.Calls);
    }

    // Los dobles fallan si una solicitud rechazada llega a la persistencia o a las imágenes.
    private sealed class UnexpectedSearch : ICardSearchRepository
    {
        public Task<CardSearchResult> SearchAsync(string actorId, CardSearchQuery query,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Consulta inesperada.");
    }

    private sealed class UnexpectedImages : IImageStore
    {
        public Task<StoredImage> SaveAsync(ImageUpload upload, string kind, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Generación de imágenes inesperada.");
        public void Delete(StoredImage image) => throw new InvalidOperationException("Eliminación inesperada.");
    }

    private sealed class ProbeTransaction(ICatalogWriteSession? session = null) : ICatalogTransaction
    {
        public int Calls { get; private set; }
        public Task<T> RunAsync<T>(string actor, string key, string operation, object data, ImageUpload image,
            Func<ICatalogWriteSession, Task<T>> create, CancellationToken cancellationToken)
        {
            Calls++;
            return create(session ?? throw new InvalidOperationException("Transacción inesperada."));
        }
    }

    private sealed class AbilitySession(AbilityItem? ability) : ICatalogWriteSession
    {
        public ICardCreateRepository Cards => throw new InvalidOperationException("Guardado inesperado.");
        public ILocationRepository Locations => throw new InvalidOperationException("Guardado inesperado.");
        public Task<AbilityItem?> FindActiveAbilityAsync(string code, CancellationToken cancellationToken)
            => Task.FromResult(ability);
    }
}
