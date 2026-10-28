
// Valores y contratos compartidos por las pantallas, los servicios y la simulación.
export const CARD_TYPES = [
  "Fuego",
  "Agua",
  "Planta",
  "Eléctrico",
  "Psíquico",
  "Lucha",
] as const;

// Deriva el tipo TypeScript del catálogo para evitar valores distintos en la interfaz.
export type CardType = (typeof CARD_TYPES)[number];

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
