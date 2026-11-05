import type { CardRarity, CardType } from "../types/catalogs";
import type { PlayerCatalogCard } from "../pages/playerCatalogTypes";
import { API_BASE_URL } from "./api";

export interface PlayerProfile {
  id: string;
  displayName: string;
  cubeBalance: number;
  elo: number;
}

export interface PlayerSignInResponse {
  accessToken: string;
  expiresAt: string;
  player: PlayerProfile;
}

export interface PlayerCatalogFilters {
  type?: CardType;
  cost?: number;
  rarity?: CardRarity;
  ownedOnly?: boolean;
  pageSize?: number;
  after?: number;
}

export interface PlayerCardPage {
  items: PlayerCatalogCard[];
  nextPosition: number | null;
  totalCount: number;
}

export interface PlayerDeck {
  id: string;
  name: string;
  cardCodes: string[];
  createdAt: string;
}

export interface CreatePlayerDeckInput {
  name: string;
  cardCodes: string[];
}

/** Expected API routes for the player-facing endpoints. */
export const PLAYER_API_ROUTES = {
  signIn: "/api/player/auth/provider",
  profile: "/api/player/me",
  catalog: "/api/player/catalog",
  collection: "/api/player/collection",
  decks: "/api/player/decks",
} as const;

let accessToken: string | null = null;

/** Keeps the platform token in memory; the provider token is never used for API requests. */
export function setPlayerAccessToken(token: string | null): void {
  accessToken = token;
}

/** Exchanges an identity-provider token for a short-lived Poké-Arena token. */
export async function signInWithProvider(
  providerToken: string,
): Promise<PlayerSignInResponse> {
  const result = await request<PlayerSignInResponse>(PLAYER_API_ROUTES.signIn, {
    method: "POST",
    body: JSON.stringify({ providerToken }),
  }, false);

  setPlayerAccessToken(result.accessToken);
  return result;
}

export function signOutPlayer(): void {
  setPlayerAccessToken(null);
}

export async function getPlayerProfile(): Promise<PlayerProfile> {
  return request<PlayerProfile>(PLAYER_API_ROUTES.profile);
}

/** Requests one server-paginated catalog page; filters are combined by the API. */
export async function getPlayerCatalog(
  filters: PlayerCatalogFilters = {},
): Promise<PlayerCardPage> {
  return request<PlayerCardPage>(
    `${PLAYER_API_ROUTES.catalog}${toQueryString(filters)}`,
  );
}

/** Requests the player's owned cards and their quantities. */
export async function getPlayerCollection(
  pageSize = 24,
  after?: number,
): Promise<PlayerCardPage> {
  return request<PlayerCardPage>(
    `${PLAYER_API_ROUTES.collection}${toQueryString({ pageSize, after })}`,
  );
}

/** Saves a 12-card deck; the server must validate ownership and deck rules. */
export async function createPlayerDeck(
  input: CreatePlayerDeckInput,
): Promise<PlayerDeck> {
  return request<PlayerDeck>(PLAYER_API_ROUTES.decks, {
    method: "POST",
    body: JSON.stringify(input),
  });
}

async function request<T>(
  path: string,
  options: RequestInit = {},
  authenticated = true,
): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set("Accept", "application/json");

  if (options.body && !(options.body instanceof FormData)) {
    headers.set("Content-Type", "application/json");
  }

  if (authenticated && accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...options,
      headers,
    });
  } catch {
    throw new PlayerApiError(0, "No se pudo conectar con la API de Poké-Arena.");
  }

  if (!response.ok) {
    let detail = "";
    try {
      const body: unknown = await response.json();
      if (body && typeof body === "object") {
        const data = body as Record<string, unknown>;
        detail = String(data.message ?? data.title ?? "");
        if (data.errors) detail += ` ${JSON.stringify(data.errors)}`;
      }
    } catch {
      // The response body may not contain JSON.
    }

    const message = response.status === 401
      ? "La sesión no es válida o ha caducado."
      : response.status === 403
        ? "No tienes permisos para esta operación."
        : detail || `Error HTTP ${response.status}`;
    throw new PlayerApiError(response.status, message);
  }

  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

function toQueryString(values: object): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== undefined && value !== null && String(value).trim() !== "") {
      params.set(key, String(value));
    }
  }
  const query = params.toString();
  return query ? `?${query}` : "";
}

export class PlayerApiError extends Error {
  readonly status: number;

  constructor(
    status: number,
    message: string,
  ) {
    super(message);
    this.name = "PlayerApiError";
    this.status = status;
  }
}
