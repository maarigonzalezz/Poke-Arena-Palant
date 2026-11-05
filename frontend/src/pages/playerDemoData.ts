import type { CardRarity, CardType } from "../types/catalogs";

export interface PlayerDemoCard {
  code: string;
  name: string;
  cost: number;
  power: number;
  type: CardType;
  rarity: CardRarity;
  ability: string;
}

export const PLAYER_DEMO_CARDS: PlayerDemoCard[] = [
  { code: "PKA-FUE-001", name: "Brasa veloz", cost: 1, power: 3, type: "Fuego", rarity: "Común", ability: "Impulso ardiente" },
  { code: "PKA-FUE-002", name: "Guardián de ceniza", cost: 2, power: 5, type: "Fuego", rarity: "Común", ability: "Manto cálido" },
  { code: "PKA-FUE-003", name: "Salamandra solar", cost: 3, power: 6, type: "Fuego", rarity: "Común", ability: "Llama persistente" },
  { code: "PKA-AGU-001", name: "Pez burbuja", cost: 1, power: 2, type: "Agua", rarity: "Común", ability: "Burbuja protectora" },
  { code: "PKA-AGU-002", name: "Tortuga de marea", cost: 2, power: 4, type: "Agua", rarity: "Común", ability: "Caparazón fluido" },
  { code: "PKA-AGU-003", name: "Mantarraya azul", cost: 3, power: 6, type: "Agua", rarity: "Común", ability: "Corriente veloz" },
  { code: "PKA-PLA-001", name: "Brote guardián", cost: 1, power: 2, type: "Planta", rarity: "Común", ability: "Raíces firmes" },
  { code: "PKA-PLA-002", name: "Musgo errante", cost: 2, power: 4, type: "Planta", rarity: "Común", ability: "Manto de hojas" },
  { code: "PKA-PLA-003", name: "Flor de rocío", cost: 3, power: 5, type: "Planta", rarity: "Común", ability: "Brote renovador" },
  { code: "PKA-ELE-001", name: "Chispa saltarina", cost: 1, power: 3, type: "Eléctrico", rarity: "Común", ability: "Descarga ligera" },
  { code: "PKA-ELE-002", name: "Rayo joven", cost: 2, power: 5, type: "Eléctrico", rarity: "Común", ability: "Carga rápida" },
  { code: "PKA-ELE-003", name: "Colibrí voltaico", cost: 3, power: 6, type: "Eléctrico", rarity: "Común", ability: "Picotazo eléctrico" },
  { code: "PKA-PSI-001", name: "Eco lunar", cost: 1, power: 2, type: "Psíquico", rarity: "Común", ability: "Eco sereno" },
  { code: "PKA-PSI-002", name: "Orbe curioso", cost: 2, power: 4, type: "Psíquico", rarity: "Común", ability: "Lectura mental" },
  { code: "PKA-LUC-001", name: "Puño aprendiz", cost: 2, power: 5, type: "Lucha", rarity: "Común", ability: "Golpe decidido" },
  { code: "PKA-FUE-004", name: "Fénix de lava", cost: 5, power: 10, type: "Fuego", rarity: "Rara", ability: "Renacer ardiente" },
  { code: "PKA-AGU-004", name: "Leviatán cristalino", cost: 6, power: 12, type: "Agua", rarity: "Legendaria", ability: "Marea profunda" },
  { code: "PKA-PLA-004", name: "Roble ancestral", cost: 4, power: 9, type: "Planta", rarity: "Poco común", ability: "Tronco protector" },
  { code: "PKA-ELE-004", name: "Tormenta celeste", cost: 7, power: 14, type: "Eléctrico", rarity: "Legendaria", ability: "Pulso de tormenta" },
  { code: "PKA-PSI-003", name: "Viajera astral", cost: 4, power: 8, type: "Psíquico", rarity: "Poco común", ability: "Salto mental" },
  { code: "PKA-LUC-002", name: "Maestro del valle", cost: 5, power: 11, type: "Lucha", rarity: "Rara", ability: "Contraataque" },
  { code: "PKA-FUE-005", name: "Zorro de brasa", cost: 3, power: 7, type: "Fuego", rarity: "Poco común", ability: "Rastro de fuego" },
  { code: "PKA-AGU-005", name: "Delfín de espuma", cost: 2, power: 5, type: "Agua", rarity: "Poco común", ability: "Salto de espuma" },
  { code: "PKA-PLA-005", name: "Liana danzante", cost: 2, power: 4, type: "Planta", rarity: "Rara", ability: "Enredadera veloz" },
];

export const INITIAL_CARD_CODES = PLAYER_DEMO_CARDS
  .filter((card) => card.rarity === "Común")
  .slice(0, 15)
  .map((card) => card.code);

export const TYPE_MARKS: Record<CardType, string> = {
  Fuego: "✦",
  Agua: "◉",
  Planta: "❋",
  "Eléctrico": "ϟ",
  "Psíquico": "✧",
  Lucha: "✹",
};
