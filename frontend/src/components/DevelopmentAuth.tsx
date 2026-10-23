
import { useState } from "react";
import { setCatalogAccessToken } from "../services/catalogApi";

/** Entrada manual de JWT para desarrollo; actualmente no está montada en App. */
export default function DevelopmentAuth() {
  const [token, setToken] = useState("");
  const [connected, setConnected] = useState(false);

  if (!import.meta.env.DEV) return null;

  function connect() {
    if (!token.trim()) return;

    setCatalogAccessToken(token.trim());
    setConnected(true);
    setToken("");
  }

  function disconnect() {
    setCatalogAccessToken(null);
    setConnected(false);
    setToken("");
  }

  return (
    <div className="panel">
      <h2>Acceso administrativo de pruebas</h2>

      <p>
        {connected
          ? "Token configurado. Ya puedes probar los endpoints protegidos."
          : "Introduce el token JWT de desarrollo para consultar el catálogo."}
      </p>

      <input
        type="password"
        autoComplete="off"
        placeholder="Token JWT de desarrollo"
        aria-label="Token JWT"
        value={token}
        onChange={(e) => setToken(e.target.value)}
        style={{
          width: "100%",
          padding: "12px",
          border: "1px solid #cbd5e1",
          borderRadius: "8px"
        }}
      />

      <div className="actions">
        <button type="button" onClick={connect}>
          Conectar
        </button>
        <button
          type="button"
          className="button-secondary"
          onClick={disconnect}
        >
          Desconectar
        </button>
      </div>
    </div>
  );
}
