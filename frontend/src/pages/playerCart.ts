import type { PlayerCatalogCard } from "./playerCatalogTypes";

export interface PlayerCartState {
  quantities: Record<string, number>;
  purchasedCodes: string[];
}

export const EMPTY_PLAYER_CART: PlayerCartState = { quantities: {}, purchasedCodes: [] };

type CartAction =
  | { type: "add" | "toggle"; code: string; cards: PlayerCatalogCard[] }
  | { type: "quantity"; code: string; quantity: number; cards: PlayerCatalogCard[] }
  | { type: "purchase"; cards: PlayerCatalogCard[] }
  | { type: "reset" };

/** Compras de demostración: solo duran la sesión y no descuentan cubos. */
export function playerCartReducer(state: PlayerCartState, action: CartAction): PlayerCartState {
  if (action.type === "reset") return EMPTY_PLAYER_CART;

  const available = (code: string) => action.cards.some(card => card.code === code);

  if (action.type !== "purchase") {
    if (!available(action.code)) return state;
    const current = state.quantities[action.code] ?? 0;
    const quantity = action.type === "quantity" ? action.quantity
      : action.type === "toggle" && current > 0 ? 0 : current || 1;
    if (!Number.isSafeInteger(quantity) || quantity < 0) return state;
    const quantities = { ...state.quantities };
    if (quantity === 0) delete quantities[action.code];
    else quantities[action.code] = quantity;
    return { ...state, quantities };
  }

  return {
    quantities: {},
    purchasedCodes: [...state.purchasedCodes, ...Object.entries(state.quantities)
      .filter(([code]) => available(code))
      .flatMap(([code, quantity]) => Array<string>(quantity).fill(code))],
  };
}
