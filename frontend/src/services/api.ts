
const API_BASE_URL =
  (import.meta.env.VITE_API_URL?.trim() ||
  "http://localhost:5003").replace(/\/+$/, "");

export interface HealthResponse {
  status: string;
  application: string;
  message: string;
}

export async function checkBackend(): Promise<HealthResponse> {
  const response = await fetch(
    `${API_BASE_URL}/api/health`,
    {
      method: "GET",
      headers: {
        Accept: "application/json",
      },
    }
  );

  if (!response.ok) {
    throw new Error(
      `Error de conexión: ${response.status}`
    );
  }

  const data: HealthResponse = await response.json();

  return data;
}
