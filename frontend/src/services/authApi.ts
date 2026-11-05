import { API_BASE_URL } from "./api";

export interface PlayerProfile {
  id: string;
  displayName: string | null;
  email: string | null;
  role: string;
  elo: number;
}

export interface LoginResult {
  accessToken: string;
  tokenType: "Bearer";
  expiresIn: number;
  expiresAt: string;
  player: PlayerProfile;
  isNewPlayer: boolean;
}

export interface LoginConfiguration {
  googleClientId: string | null;
  enabled: boolean;
  accessTokenLifetimeSeconds: number;
}

// Este cliente siempre usa HTTP real y mantiene los tokens fuera de cookies y almacenamiento local.
async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(`${API_BASE_URL}/api/auth/${path}`, {
    ...options, cache: "no-store", credentials: "omit",
  });
  if (!response.ok) {
    const body = await response.json().catch(() => null) as { message?: string; title?: string } | null;
    throw new Error(body?.message ?? body?.title ?? `No se pudo completar el acceso (${response.status}).`);
  }
  return response.json() as Promise<T>;
}

export const getLoginConfiguration = (signal?: AbortSignal) =>
  request<LoginConfiguration>("config", { signal });

export const loginWithGoogle = (idToken: string, signal?: AbortSignal) =>
  request<LoginResult>("google", {
    method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ idToken }), signal,
  });

export const getCurrentPlayer = (accessToken: string, signal?: AbortSignal) =>
  request<PlayerProfile>("me", { headers: { Authorization: `Bearer ${accessToken}` }, signal });
