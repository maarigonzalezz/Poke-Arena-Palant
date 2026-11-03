
// Valores y contratos compartidos por las pantallas, los servicios y la simulación.
// Nombres y abreviaturas se declaran juntos para reutilizarlos en pantallas y mocks.
export const CARD_TYPE_PREFIXES = Object.freeze({
  Fuego: "FUE", Agua: "AGU", Planta: "PLA",
  Eléctrico: "ELE", Psíquico: "PSI", Lucha: "LUC",
} as const);

export type CardType = keyof typeof CARD_TYPE_PREFIXES;

// Conserva el orden de las opciones y evita mantener una segunda lista manual.
export const CARD_TYPES = Object.freeze(Object.keys(CARD_TYPE_PREFIXES) as CardType[]);

export const RARITIES = [
  "Común",
  "Poco común",
  "Rara",
  "Legendaria",
] as const;

export type CardRarity = (typeof RARITIES)[number];

export const EFFECT_TYPES = [
  "None",
  "TypeBonus",
  "CostModifier",
  "TurnRestriction",
] as const;

export type EffectType = (typeof EFFECT_TYPES)[number];

export type EnergyMode = "Fixed" | "PerCard";

// Incluye parámetros del efecto y la restricción opcional por tipo de carta.
export interface Ability {
  code: string;
  name: string;
  trigger: string | null;
  effect: string | null;
  parameters: Record<string, unknown>;
  restrictedCardType?: CardType | null;
}

// Contrato de lectura utilizado para dibujar una carta en el catálogo.
export interface Card {
  code: string;
  name: string;
  cost: number;
  power: number;
  type: CardType;
  rarity: CardRarity;
  abilityCode: string;
  abilityName: string;
  abilityTrigger: string | null;
  abilityEffect: string | null;
  abilityParameters: string | Record<string, unknown>;
  imageStatus: string;
  imageUrl: string | null;
}

/** Filtros combinados con AND; al continuar se envían solo searchId, after y pageSize. */
export interface CardFilters {
  code?: string;
  name?: string;
  cost?: number;
  minCost?: number;
  maxCost?: number;
  power?: number;
  minPower?: number;
  maxPower?: number;
  type?: CardType;
  rarity?: CardRarity;
  abilityCode?: string;
  abilityTrigger?: string;
  abilityEffect?: string;
  pageSize?: number;
  searchId?: string;
  after?: number;
}

/** Página de resultados; nextPosition nulo indica que no quedan más elementos. */
export interface CardSearchResult {
  items: Card[];
  searchId: string;
  nextPosition: number | null;
  totalCount: number;
}

// Contrato de escritura: incluye la imagen original, pero no un código asignado.
export interface CreateCardInput {
  name: string;
  cost: number;
  power: number;
  type: CardType;
  rarity: CardRarity;
  abilityCode: string;
  image: File;
}

// La configuración del efecto viaja como objeto hasta serializarse en el servicio.
export interface CreateLocationInput {
  name: string;
  description: string;
  maxCards: number;
  energyMode: EnergyMode;
  extraEnergy: number;
  effectType: EffectType;
  effectParameters: Record<string, unknown>;
  image: File;
}
