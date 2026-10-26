
import { useState } from "react";
import { getAbilities, setCatalogAccessToken } from "../services/catalogApi";

/** Conecta un JWT real durante desarrollo y recarga los catálogos al cambiar de sesión. */
export default function DevelopmentAuth({ onChange }: { onChange: () => void }) {
  const [token, setToken] = useState("");
  const [connected, setConnected] = useState(false);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  if (!import.meta.env.DEV) return null;

  async function connect() {
    if (!token.trim() || loading) return;
    setLoading(true);
    setError("");
    setCatalogAccessToken(token.trim());
    try {
      await getAbilities();
      setConnected(true);
      setToken("");
    } catch (cause) {
      setCatalogAccessToken(null);
      setConnected(false);
      setError(cause instanceof Error ? cause.message : "No se pudo verificar la sesión.");
    } finally {
      setLoading(false);
      onChange();
    }
  }

  function disconnect() {
    setCatalogAccessToken(null);
    setConnected(false);
    setToken("");
    setError("");
    onChange();
  }

  return (
    <div className={`panel auth-panel ${connected ? "is-connected" : ""}`}>
      <div className="auth-description">
        <h2>{connected ? "Sesión administrativa activa" : "Acceso administrativo"}</h2>
        <p>
          {connected
            ? "Puedes consultar el catálogo y crear contenido."
            : "Conecta tu token de desarrollo para administrar el catálogo."}
        </p>
      </div>
      <div className="auth-controls">
        {!connected && <>
          <input
            type="password"
            autoComplete="off"
            placeholder="Token JWT de desarrollo"
            aria-label="Token JWT"
            value={token}
            onChange={(e) => setToken(e.target.value)}
            className="token-input"
          />
          <div className="actions">
            <button type="button" disabled={loading || !token.trim()} onClick={() => void connect()}>
              {loading ? "Verificando..." : "Conectar"}
            </button>
          </div>
        </>}
        {connected && <div className="actions">
          <button
            type="button"
            className="button-secondary"
            disabled={loading}
            onClick={disconnect}
          >
            Desconectar
          </button>
        </div>}
      </div>
      {error && <p role="alert" className="alert error">{error}</p>}
    </div>
  );
}
