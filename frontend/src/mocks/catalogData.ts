
import type { Ability, Card } from "../types/catalogs";

// Catálogo fijo de demostración, independiente de los registros de PostgreSQL.
export const MOCK_ABILITIES: Ability[] = [
  {
    code: "HAB-000",
    name: "Sin Habilidad",
    trigger: null,
    effect: null,
    parameters: {},
    restrictedCardType: null,
  },
  {
    code: "HAB-001",
    name: "Ataque ígneo",
    trigger: "AlRevelarse",
    effect: "AumentarPoder",
    parameters: { power: 3 },
    restrictedCardType: "Fuego",
  },
  {
    code: "HAB-002",
    name: "Escudo acuático",
    trigger: "Continuo",
    effect: "AumentarPoder",
    parameters: { power: 2 },
    restrictedCardType: "Agua",
  },
  {
    code: "HAB-003",
    name: "Evolución",
    trigger: "Evolucion",
    effect: "Evolucionar",
    parameters: { targetCode: "PKA-FUE-002" },
    restrictedCardType: "Fuego",
  },
];

type SeedCard = {
  code: string;
  name: string;
  cost: number;
  power: number;
  type: Card["type"];
  rarity: Card["rarity"];
  abilityCode: string;
};

const SEED: SeedCard[] = [
  { code: "PKA-FUE-004", name: "Volcán Ágil", cost: 1, power: 3, type: "Fuego", rarity: "Rara", abilityCode: "HAB-001" },
  { code: "PKA-AGU-004", name: "Marea Fría", cost: 3, power: 10, type: "Agua", rarity: "Rara", abilityCode: "HAB-002" },
  { code: "PKA-PLA-004", name: "Raíz Antigua", cost: 0, power: -20, type: "Planta", rarity: "Común", abilityCode: "HAB-000" },
  { code: "PKA-ELE-003", name: "Trueno", cost: 7, power: 16, type: "Eléctrico", rarity: "Legendaria", abilityCode: "HAB-000" },
  { code: "PKA-PSI-003", name: "Sueño", cost: 5, power: 8, type: "Psíquico", rarity: "Rara", abilityCode: "HAB-000" },
  { code: "PKA-LUC-003", name: "Puño Firme", cost: 2, power: 4, type: "Lucha", rarity: "Común", abilityCode: "HAB-000" },
  {
    code: "PKA-FUE-001", name: "Charmander",
    cost: 2, power: 4, type: "Fuego",
    rarity: "Común", abilityCode: "HAB-001",
  },
  {
    code: "PKA-FUE-002", name: "Charmeleon",
    cost: 4, power: 8, type: "Fuego",
    rarity: "Poco común", abilityCode: "HAB-000",
  },
  {
    code: "PKA-FUE-003", name: "Charizard",
    cost: 6, power: 15, type: "Fuego",
    rarity: "Legendaria", abilityCode: "HAB-001",
  },
  {
    code: "PKA-AGU-001", name: "Squirtle",
    cost: 2, power: 3, type: "Agua",
    rarity: "Común", abilityCode: "HAB-002",
  },
  {
    code: "PKA-AGU-002", name: "Wartortle",
    cost: 4, power: 7, type: "Agua",
    rarity: "Poco común", abilityCode: "HAB-000",
  },
  {
    code: "PKA-AGU-003", name: "Blastoise",
    cost: 6, power: 14, type: "Agua",
    rarity: "Rara", abilityCode: "HAB-002",
  },
  {
    code: "PKA-PLA-001", name: "Bulbasaur",
    cost: 1, power: 2, type: "Planta",
    rarity: "Común", abilityCode: "HAB-000",
  },
  {
    code: "PKA-PLA-002", name: "Ivysaur",
    cost: 3, power: 6, type: "Planta",
    rarity: "Poco común", abilityCode: "HAB-000",
  },
  {
    code: "PKA-PLA-003", name: "Venusaur",
    cost: 5, power: 12, type: "Planta",
    rarity: "Rara", abilityCode: "HAB-000",
  },
  {
    code: "PKA-ELE-001", name: "Pikachu",
    cost: 3, power: 7, type: "Eléctrico",
    rarity: "Rara", abilityCode: "HAB-000",
  },
  {
    code: "PKA-ELE-002", name: "Raichu",
    cost: 5, power: 10, type: "Eléctrico",
    rarity: "Poco común", abilityCode: "HAB-000",
  },
  {
    code: "PKA-PSI-001", name: "Mew",
    cost: 10, power: 20, type: "Psíquico",
    rarity: "Legendaria", abilityCode: "HAB-000",
  },
  {
    code: "PKA-PSI-002", name: "Abra",
    cost: 0, power: -2, type: "Psíquico",
    rarity: "Común", abilityCode: "HAB-000",
  },
  {
    code: "PKA-LUC-001", name: "Machop",
    cost: 2, power: 5, type: "Lucha",
    rarity: "Común", abilityCode: "HAB-000",
  },
  {
    code: "PKA-LUC-002", name: "Machoke",
    cost: 4, power: 9, type: "Lucha",
    rarity: "Rara", abilityCode: "HAB-000",
  },
];

/** Construye 21 cartas con sus habilidades para probar filtros y una segunda página. */
export function buildMockCards(): Card[] {
  return SEED.map((card) => {
    const ability = MOCK_ABILITIES.find(
      (item) => item.code === card.abilityCode
    )!;

    return {
      ...card,
      abilityName: ability.name,
      abilityTrigger: ability.trigger,
      abilityEffect: ability.effect,
      abilityParameters: ability.parameters,
      imageStatus: "Pending",
      imageUrl: null,
    };
  });
}
