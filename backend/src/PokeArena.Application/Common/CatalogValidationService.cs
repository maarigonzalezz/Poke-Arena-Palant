using PokeArena.Application.Cards;
using PokeArena.Application.Locations;

namespace PokeArena.Application.Common;

/// <summary>Entrada común para validar solicitudes; no ejecuta SQL ni guarda archivos.</summary>
public sealed class CatalogValidationService
{
    public void ValidateSearch(CardSearchQuery query)
    {
        var errors = CardSearchValidator.Validate(query);
        if (errors.Count > 0)
            throw new CatalogValidationException(errors, "invalid_search_criteria", "Criterios de búsqueda inválidos.");
    }

    public void ValidateCard(CardCreateQuery query, string key)
    {
        // Todas las reglas de campos se comprueban antes de abrir la transacción.
        var errors = CardCreateValidator.Validate(query);
        ValidateKey(key, errors);
        if (errors.Count > 0) throw new CatalogValidationException(errors);
    }

    public AbilityItem ValidateAbility(CardCreateQuery query, AbilityItem? ability)
    {
        // El repositorio entrega datos; esta clase decide si permiten crear la carta.
        if (ability is null)
            throw new CatalogValidationException(new() { ["abilityCode"] = "La habilidad no existe o está inactiva." });
        if (ability.RestrictedCardType is not null && ability.RestrictedCardType != query.Type)
            throw new CatalogValidationException(new() { ["abilityCode"] = "La habilidad no corresponde al tipo de carta." });
        return ability;
    }

    public void ValidateLocation(CreateLocationRequest request, string key)
    {
        var errors = new Dictionary<string, string>();
        var locationErrors = LocationValidator.Validate(request);
        if (locationErrors.Count > 0) errors["location"] = string.Join(" ", locationErrors);
        ValidateKey(key, errors);
        if (errors.Count > 0) throw new CatalogValidationException(errors);
    }

    // La clave forma parte de la solicitud, no de las reglas de persistencia.
    private static void ValidateKey(string key, Dictionary<string, string> errors)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100)
            errors["idempotencyKey"] = "Idempotency-Key es obligatorio y admite hasta 100 caracteres.";
    }
}
