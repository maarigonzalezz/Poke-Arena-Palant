import type { PlayerCatalogCard } from "./playerCatalogTypes";

export interface PlayerCartState {
  cartCodes: string[];
  purchasedCodes: string[];
}

export const EMPTY_PLAYER_CART: PlayerCartState = { cartCodes: [], purchasedCodes: [] };

type CartAction =
  | { type: "toggle"; code: string; cards: PlayerCatalogCard[] }
  | { type: "purchase"; cards: PlayerCatalogCard[] }
  | { type: "reset" };

/** Compras de demostración: solo duran la sesión y no descuentan cubos. */
export function playerCartReducer(state: PlayerCartState, action: CartAction): PlayerCartState {
  if (action.type === "reset") return EMPTY_PLAYER_CART;

  const available = (code: string) => action.cards.some(card =>
    card.code === code && card.quantity === 0 && !state.purchasedCodes.includes(code));

  if (action.type === "toggle") {
    if (state.cartCodes.includes(action.code)) {
      return { ...state, cartCodes: state.cartCodes.filter(code => code !== action.code) };
    }
    if (!available(action.code)) return state;
    return { ...state, cartCodes: [...state.cartCodes, action.code] };
  }

  return {
    cartCodes: [],
    purchasedCodes: [...state.purchasedCodes, ...state.cartCodes.filter(available)],
  };
}
