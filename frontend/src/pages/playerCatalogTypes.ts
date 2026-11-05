import type { Card, CardType } from "../types/catalogs";

export type PlayerCatalogCard = Card & {
  quantity: number;
};

export const TYPE_MARKS: Record<CardType, string> = {
  Fuego: "✦",
  Agua: "◉",
  Planta: "❋",
  "Eléctrico": "ϟ",
  "Psíquico": "✧",
  Lucha: "✹",
};
