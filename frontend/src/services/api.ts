
const API_URL =
  import.meta.env.VITE_API_URL ??
  "http://localhost:5000";

export async function checkBackend() {
  const response = await fetch(
    `${API_URL}/api/health`
  );

  if (!response.ok) {
    throw new Error(
      "No fue posible conectar con el backend"
    );
  }

  return response.json();
}
