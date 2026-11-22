import type { CardRarity, CardType } from "../types/catalogs";
import type { PlayerCatalogCard } from "./playerCatalogTypes";

type CardSeed = {
  code: string;
  name: string;
  cost: number;
  power: number;
  type: CardType;
  rarity: CardRarity;
  abilityName: string;
};

const CARD_SEEDS: CardSeed[] = [
  { code: "PKA-FUE-001", name: "Brasa veloz", cost: 1, power: 3, type: "Fuego", rarity: "Común", abilityName: "Impulso ardiente" },
  { code: "PKA-FUE-002", name: "Guardián de ceniza", cost: 2, power: 5, type: "Fuego", rarity: "Común", abilityName: "Manto cálido" },
  { code: "PKA-FUE-003", name: "Salamandra solar", cost: 3, power: 6, type: "Fuego", rarity: "Común", abilityName: "Llama persistente" },
  { code: "PKA-AGU-001", name: "Pez burbuja", cost: 1, power: 2, type: "Agua", rarity: "Común", abilityName: "Burbuja protectora" },
  { code: "PKA-AGU-002", name: "Tortuga de marea", cost: 2, power: 4, type: "Agua", rarity: "Común", abilityName: "Caparazón fluido" },
  { code: "PKA-AGU-003", name: "Mantarraya azul", cost: 3, power: 6, type: "Agua", rarity: "Común", abilityName: "Corriente veloz" },
  { code: "PKA-PLA-001", name: "Brote guardián", cost: 1, power: 2, type: "Planta", rarity: "Común", abilityName: "Raíces firmes" },
  { code: "PKA-PLA-002", name: "Musgo errante", cost: 2, power: 4, type: "Planta", rarity: "Común", abilityName: "Manto de hojas" },
  { code: "PKA-PLA-003", name: "Flor de rocío", cost: 3, power: 5, type: "Planta", rarity: "Común", abilityName: "Brote renovador" },
  { code: "PKA-ELE-001", name: "Chispa saltarina", cost: 1, power: 3, type: "Eléctrico", rarity: "Común", abilityName: "Descarga ligera" },
  { code: "PKA-ELE-002", name: "Rayo joven", cost: 2, power: 5, type: "Eléctrico", rarity: "Común", abilityName: "Carga rápida" },
  { code: "PKA-ELE-003", name: "Colibrí voltaico", cost: 3, power: 6, type: "Eléctrico", rarity: "Común", abilityName: "Picotazo eléctrico" },
  { code: "PKA-PSI-001", name: "Eco lunar", cost: 1, power: 2, type: "Psíquico", rarity: "Común", abilityName: "Eco sereno" },
  { code: "PKA-PSI-002", name: "Orbe curioso", cost: 2, power: 4, type: "Psíquico", rarity: "Común", abilityName: "Lectura mental" },
  { code: "PKA-LUC-001", name: "Puño aprendiz", cost: 2, power: 5, type: "Lucha", rarity: "Común", abilityName: "Golpe decidido" },
  { code: "PKA-FUE-004", name: "Fénix de lava", cost: 5, power: 10, type: "Fuego", rarity: "Rara", abilityName: "Renacer ardiente" },
  { code: "PKA-AGU-004", name: "Leviatán cristalino", cost: 6, power: 12, type: "Agua", rarity: "Legendaria", abilityName: "Marea profunda" },
  { code: "PKA-PLA-004", name: "Roble ancestral", cost: 4, power: 9, type: "Planta", rarity: "Poco común", abilityName: "Tronco protector" },
  { code: "PKA-ELE-004", name: "Tormenta celeste", cost: 7, power: 14, type: "Eléctrico", rarity: "Legendaria", abilityName: "Pulso de tormenta" },
  { code: "PKA-PSI-003", name: "Viajera astral", cost: 4, power: 8, type: "Psíquico", rarity: "Poco común", abilityName: "Salto mental" },
  { code: "PKA-LUC-002", name: "Maestro del valle", cost: 5, power: 11, type: "Lucha", rarity: "Rara", abilityName: "Contraataque" },
  { code: "PKA-FUE-005", name: "Zorro de brasa", cost: 3, power: 7, type: "Fuego", rarity: "Poco común", abilityName: "Rastro de fuego" },
  { code: "PKA-AGU-005", name: "Delfín de espuma", cost: 2, power: 5, type: "Agua", rarity: "Poco común", abilityName: "Salto de espuma" },
  { code: "PKA-PLA-005", name: "Liana danzante", cost: 2, power: 4, type: "Planta", rarity: "Rara", abilityName: "Enredadera veloz" },
];

export const PLAYER_TEST_CARDS: PlayerCatalogCard[] = CARD_SEEDS.map((card, index) => ({
  ...card,
  abilityCode: "HAB-000",
  abilityTrigger: null,
  abilityEffect: null,
  abilityParameters: {},
  imageStatus: "Ready",
  imageUrl: null,
  quantity: index < 15 ? 1 : 0,
}));

export const STARTING_CUBE_BALANCE = 1000;
