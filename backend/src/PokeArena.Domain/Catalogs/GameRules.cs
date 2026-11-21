namespace PokeArena.Domain.Catalogs;

// Cantidades compartidas por los casos de uso y los adaptadores de persistencia.
public static class GameRules
{
    public const int InitialCollectionSize = 15;
    public const int InitialCubes = 1000;
    public const int DeckSize = 12;
    public const int InitialMovementSequence = 1;
    public const int CubeHistoryPageSize = 50;
    public const int DefaultPageSize = 12;
    public const int MaximumPageSize = 100;
    public const int AdminPageSize = 20;
    public const string CommonRarity = "Común";
    public const string InitialCollectionReason = "InitialCollection";
    public const string ReadyImageStatus = "Ready";
    public const string PlayerRole = "Player";
    public const int InitialElo = 0;
    public const int SearchSessionLifetimeMinutes = 30;
    public const int MaximumNameLength = 120;
    public const int MaximumDescriptionLength = 10000;
    public const int MaximumEffectParametersLength = 4096;
    public const int MinimumCost = 0;
    public const int MaximumCost = 10;
    public const int MinimumPower = -20;
    public const int MaximumPower = 20;
    public const int MaximumCardsPerLocation = 5;
    public const int MaximumFixedEnergy = 3;
    public const int MaximumPerCardEnergy = 2;
}
