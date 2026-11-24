import type { Card, CardRarity, CardType } from "../types/catalogs";
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
  cards?: DeckCardPreview[] | null;
}

export type DeckCardPreview = Pick<Card, "code" | "name" | "type" | "cost" | "power" | "imageUrl" | "imageSmallUrl">;

export interface CreatePlayerDeckInput {
  name: string;
  cardCodes: string[];
}

export interface MatchmakingDeckOption {
  id: string;
  name: string;
}

/** Búsqueda del servidor; incluye solo el deck seleccionado y el estado público del emparejamiento. */
export interface MatchSearch {
  id: string;
  status: "Searching" | "Matched" | "Cancelled" | "Expired";
  context: "Ranked" | "Tournament";
  contextId: string | null;
  deckId: string;
  elo: number;
  acceptedEloDifference: number;
  createdAt: string;
  matchId: string | null;
  message: string | null;
  botAvailable: boolean;
}

export interface MatchLocationState {
  slot: number;
  id: number;
  name: string;
  description: string;
  maxCards: number;
  energyMode: "Fixed" | "PerCard";
  extraEnergy: number;
  effectType: string;
  restrictedTurns: number[];
  imageSmallPath: string | null;
}

export interface MatchCardState {
  id: number;
  name: string;
  cost: number;
  power: number;
  cardType: string;
  imagePath: string | null;
  imageSmallPath: string | null;
}

export interface MatchBoardCardState {
  id: number;
  name: string;
  cost: number;
  power: number;
  locationSlot: number;
  imagePath: string | null;
}

export interface MatchPlayerState {
  playerId: string;
  isBot: boolean;
  handCards: MatchCardState[];
  cemeteryCardIds: number[];
  cemeteryCards: MatchCardState[];
  boardCards: MatchBoardCardState[];
  energy: number;
  remainingDeckCards: number;
}

// El estado público no incluye las cartas de la mano ni el orden del mazo del rival.
export interface MatchOpponentState {
  isBot: boolean;
  handCardCount: number;
  cemeteryCards: MatchCardState[];
  boardCards: MatchBoardCardState[];
  energy: number;
  remainingDeckCards: number;
}

export interface MatchResultState {
  outcome: "Win" | "Loss" | "Draw" | "Completed";
  playerLocationsWon: number;
  opponentLocationsWon: number;
  playerTotalPower: number;
  opponentTotalPower: number;
  playerFinalEnergy: number;
  opponentFinalEnergy: number;
  playerCemeteryCount: number;
  opponentCemeteryCount: number;
  cubesAwarded: number;
}

/** Snapshot de una partida para un participante; los datos ocultos del rival no forman parte del contrato. */
export interface MatchState {
  matchId: string;
  context: "Ranked" | "Tournament";
  status: "Active" | "Completed" | "Cancelled";
  isBot: boolean;
  turnNumber: number;
  turnDurationSeconds: number;
  turnStartedAt: string | null;
  turnEndsAt: string | null;
  wagerCubes: number;
  locations: MatchLocationState[];
  player: MatchPlayerState;
  opponent: MatchOpponentState;
  result: MatchResultState | null;
}

// Solo consulta los mazos existentes; su creación pertenece al módulo de mazos.
/** Obtiene los mazos disponibles del jugador sin usar respuestas cacheadas. */
export function getMatchmakingDecks(signal?: AbortSignal): Promise<MatchmakingDeckOption[]> {
  return request("/api/player/matchmaking/decks", { signal, cache: "no-store" });
}

/** Envía únicamente el deck elegido; el servidor valida propiedad y crea la búsqueda. */
export function startRankedSearch(deckId: string, signal?: AbortSignal): Promise<MatchSearch> {
  return request("/api/player/matchmaking/ranked", {
    method: "POST", body: JSON.stringify({ deckId }), signal,
  });
}

/** Recupera la búsqueda del jugador, o null si todavía no existe. */
export async function getCurrentMatchSearch(signal?: AbortSignal): Promise<MatchSearch | null> {
  return await request<MatchSearch | undefined>("/api/player/matchmaking/current", { signal, cache: "no-store" }) ?? null;
}

/** Solicita cancelar una búsqueda abierta asociada al jugador autenticado. */
export function cancelMatchSearch(searchId: string, signal?: AbortSignal): Promise<MatchSearch> {
  return request(`/api/player/matchmaking/${encodeURIComponent(searchId)}`, { method: "DELETE", signal });
}

// El servidor confirma la oferta y crea la partida con el deck ya reservado.
/** Acepta en el servidor una oferta de bot disponible para la búsqueda indicada. */
export function acceptBotMatch(searchId: string, signal?: AbortSignal): Promise<MatchSearch> {
  return request(`/api/player/matchmaking/${encodeURIComponent(searchId)}/bot`, { method: "POST", signal });
}

/** Lee el estado actualizado y privado para el participante de la partida. */
export function getMatchState(matchId: string, signal?: AbortSignal): Promise<MatchState> {
  return request(`/api/player/matchmaking/${encodeURIComponent(matchId)}/state`, { signal, cache: "no-store" });
}

// Los comandos modifican el estado en el servidor; la respuesta es el snapshot que debe renderizar la arena.
/** Pide avanzar el turno indicado y devuelve el snapshot resultante del servidor. */
export function advanceMatchTurn(matchId: string, turnNumber: number): Promise<MatchState> {
  return request(`/api/player/matchmaking/${encodeURIComponent(matchId)}/turn/advance`, {
    method: "POST", body: JSON.stringify({ turnNumber }),
  });
}

/** Juega una carta del jugador en una ubicación; costo y reglas se validan en la API. */
export function playMatchCard(matchId: string, cardId: number, locationSlot: number): Promise<MatchState> {
  return request(`/api/player/matchmaking/${encodeURIComponent(matchId)}/cards/play`, {
    method: "POST", body: JSON.stringify({ cardId, locationSlot }),
  });
}

/** Descarta una carta de la mano al cementerio y devuelve el estado actualizado. */
export function discardMatchCard(matchId: string, cardId: number): Promise<MatchState> {
  return request(`/api/player/matchmaking/${encodeURIComponent(matchId)}/cards/discard`, {
    method: "POST", body: JSON.stringify({ cardId }),
  });
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
  signal?: AbortSignal,
): Promise<PlayerCardPage> {
  return readCardPage(
    `${PLAYER_API_ROUTES.catalog}${toQueryString(filters)}`,
    signal,
  );
}

/** Requests the player's owned cards and their quantities. */
export async function getPlayerCollection(
  pageSize = 24,
  after?: number,
  signal?: AbortSignal,
): Promise<PlayerCardPage> {
  return readCardPage(
    `${PLAYER_API_ROUTES.collection}${toQueryString({ pageSize, after })}`,
    signal,
  );
}

/** Las rutas /files pertenecen a la API, aunque React se sirva en otro puerto. */
async function readCardPage(path: string, signal?: AbortSignal): Promise<PlayerCardPage> {
  const page = await request<PlayerCardPage>(path, { signal, cache: "no-store" });
  return { ...page, items: page.items.map(card => ({
    ...card,
    imageUrl: card.imageUrl ? new URL(card.imageUrl, `${API_BASE_URL}/`).href : null,
    imageSmallUrl: card.imageSmallUrl ? new URL(card.imageSmallUrl, `${API_BASE_URL}/`).href : null,
  })) };
}

export function getPlayerDecks(signal?: AbortSignal): Promise<PlayerDeck[]> {
  return request(PLAYER_API_ROUTES.decks, { signal, cache: "no-store" });
}

export async function createPlayerDeck(
  input: CreatePlayerDeckInput,
  idempotencyKey: string,
): Promise<PlayerDeck> {
  return request<PlayerDeck>(PLAYER_API_ROUTES.decks, {
    method: "POST",
    headers: { "Idempotency-Key": idempotencyKey },
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
  } catch (cause) {
    if (options.signal?.aborted) throw cause;
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
