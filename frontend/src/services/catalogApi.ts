
import type {
  Ability,
  CardFilters,
  CardSearchResult,
  CreateCardInput,
  CreateLocationInput,
} from "../types/catalogs";
import { API_BASE_URL } from "./api";

import {
  mockSearchCards,
  mockGetAbilities,
  mockCreateCard,
  mockCreateLocation,
} from "../mocks/mockCatalogApi";

// La simulación solo se habilita en desarrollo, por modo de Vite o variable de entorno.
export const IS_MOCK_MODE =
  import.meta.env.DEV &&
  (import.meta.env.MODE === "mock" || import.meta.env.VITE_MOCK_API === "true");

/** Error de la API; status 0 representa un fallo de conexión. */
export class ApiError extends Error {
  readonly status: number;

  constructor(
    status: number,
    message: string
  ) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

let accessToken: string | null = null;

/** Mantiene el JWT en memoria hasta reemplazarlo, desconectarse o recargar. */
export function setCatalogAccessToken(
  token: string | null
): void {
  accessToken = token;
}

// Centraliza la autenticación y convierte los errores HTTP en mensajes para la interfaz.
async function request<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const headers = new Headers(options.headers);

  // Authorization identifica al administrador en cada solicitud real.
  if (accessToken) {
    headers.set(
      "Authorization",
      `Bearer ${accessToken}`
    );
  }

  let response: Response;

  try {
    response = await fetch(
      `${API_BASE_URL}${path}`,
      { ...options, headers }
    );
  // Un fallo de red se diferencia de una respuesta HTTP de la API.
  } catch {
    throw new ApiError(
      0,
      "No se pudo conectar con la API de Poké-Arena."
    );
  }

  // Extrae el mensaje y los errores por campo que devuelve el servidor.
  if (!response.ok) {
    let detail = "";

    try {
      const body: unknown = await response.json();

      if (body && typeof body === "object") {
        const data = body as Record<string, unknown>;

        detail = String(
          data.message ?? data.title ?? ""
        );

        if (data.errors) {
          detail += ` ${JSON.stringify(data.errors)}`;
        }
      }
    } catch {
      // El servidor no devolvió JSON.
    }

    // Distingue falta de autenticación de falta de permisos.
    const message =
      response.status === 401
        ? "Debe iniciar sesión para continuar."
        : response.status === 403
          ? "No tiene permisos para esta operación."
          : detail || `Error HTTP ${response.status}`;

    throw new ApiError(response.status, message);
  }

  // Entrega la respuesta JSON al consumidor con el tipo esperado.
  return (await response.json()) as T;
}

// Omite filtros vacíos sin descartar valores numéricos como cero.
function queryString(filters: CardFilters): string {
  // Codifica los filtros para que tildes y caracteres especiales sean válidos en la URL.
  const params = new URLSearchParams();

  for (const [key, value] of Object.entries(filters)) {
    if (
      value !== undefined &&
      value !== null &&
      String(value).trim() !== ""
    ) {
      params.set(key, String(value));
    }
  }

  return params.toString();
}

/** Inicia una búsqueda o continúa su sesión mediante la API o los mocks. */
export async function searchCards(
  filters: CardFilters
): Promise<CardSearchResult> {
  // En simulación se devuelve el resultado local sin hacer una petición HTTP.
  if (IS_MOCK_MODE) {
    return mockSearchCards(filters);
  }

  return request<CardSearchResult>(
    `/api/cards?${queryString(filters)}`
  );
}

/** Obtiene las habilidades disponibles para el formulario de cartas. */
export async function getAbilities(): Promise<Ability[]> {
  if (IS_MOCK_MODE) {
    return mockGetAbilities();
  }

  return request<Ability[]>("/api/abilities");
}

/** Envía datos e imagen; conserva la clave para reintentar una misma operación. */
export async function createCard(
  data: CreateCardInput,
  idempotencyKey: string
): Promise<unknown> {
  if (IS_MOCK_MODE) {
    return mockCreateCard(data, idempotencyKey);
  }

  // FormData transporta campos y archivo; el navegador define el límite multipart.
  const form = new FormData();

  form.append("name", data.name);
  form.append("cost", String(data.cost));
  form.append("power", String(data.power));
  form.append("type", data.type);
  form.append("rarity", data.rarity);
  form.append("abilityCode", data.abilityCode);
  // Envía el archivo original para que el servidor valide y genere su miniatura.
  form.append("image", data.image);

  return request("/api/cards", {
    method: "POST",
    headers: {
      "Idempotency-Key": idempotencyKey,
    },
    body: form,
  });
}

/** Envía la ubicación, sus efectos y la imagen original como formulario multipart. */
export async function createLocation(
  data: CreateLocationInput,
  idempotencyKey: string
): Promise<unknown> {
  if (IS_MOCK_MODE) {
    return mockCreateLocation(data, idempotencyKey);
  }

  const form = new FormData();

  form.append("name", data.name);
  form.append("description", data.description);
  form.append("maxCards", String(data.maxCards));
  form.append("energyMode", data.energyMode);
  form.append("extraEnergy", String(data.extraEnergy));
  form.append("effectType", data.effectType);
  form.append(
    "effectParameters",
    // Serializa los parámetros del efecto dentro del formulario multipart.
    JSON.stringify(data.effectParameters)
  );
  form.append("image", data.image);

  return request("/api/locations", {
    method: "POST",
    headers: {
      "Idempotency-Key": idempotencyKey,
    },
    body: form,
  });
}
