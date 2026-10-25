
import { useState } from "react";
import SearchCardPage from "./pages/SearchCardPage";
import CreateCardPage from "./pages/CreateCardPage";
import CreateLocationPage from "./pages/CreateLocationPage";
import { IS_MOCK_MODE } from "./services/catalogApi";
import { resetMockCatalog } from "./mocks/mockCatalogApi";
import DevelopmentAuth from "./components/DevelopmentAuth";
import "./App.css";

type Page = "search" | "card" | "location";

const pages: Record<Page, string> = {
  search: "Buscar Carta",
  card: "Crear Carta",
  location: "Crear Ubicación",
};

/** Organiza las pantallas del catálogo y los controles del modo simulado. */
export default function App() {
  const [page, setPage] = useState<Page>("search");
  const [version, setVersion] = useState(0);
  const [resetting, setResetting] = useState(false);
  const [resetError, setResetError] = useState("");

  // Espera las escrituras locales y remonta la pantalla para descartar su estado anterior.
  async function resetSimulation() {
    if (!window.confirm("¿Restablecer solo las cartas y ubicaciones simuladas de este navegador?")) return;
    setResetting(true);
    setResetError("");
    try {
      await resetMockCatalog();
      setVersion(value => value + 1);
      setPage("search");
    } catch {
      setResetError("No se pudieron restablecer los datos del navegador.");
    } finally { setResetting(false); }
  }

  return (
    <div className="admin-app">
      <aside className="sidebar">
        <div className="brand">
          <strong>Poké-Arena</strong>
          <span>Panel de Administración</span>
        </div>

        <nav aria-label="Administración del catálogo">
          {(Object.entries(pages) as [Page, string][]).map(
            ([key, label]) => (
              <button
                key={key}
                type="button"
                className={page === key ? "nav-active" : ""}
                onClick={() => setPage(key)}
                aria-current={page === key ? "page" : undefined}
              >
                {label}
              </button>
            )
          )}
        </nav>

        <p className="sidebar-caption">
          Sprint 1 · F-02 y F-03
        </p>
      </aside>

      <main className="main-content">
        {!IS_MOCK_MODE && import.meta.env.DEV && (
          <DevelopmentAuth onChange={() => setVersion(value => value + 1)} />
        )}
        {IS_MOCK_MODE && (
          <div className="simulation-banner" role="status">
            <strong>Modo simulación activo.</strong>{" "}
            Los registros se guardan en este navegador.
            No se están utilizando PostgreSQL ni MongoDB.
            <div className="actions">
              <button type="button" disabled={resetting} onClick={() => void resetSimulation()}>
                {resetting ? "Restableciendo..." : "Restablecer datos simulados"}
              </button>
            </div>
            {resetError && <p role="alert">{resetError}</p>}
          </div>
        )}

        {page === "search" && <SearchCardPage key={version} />}
        {page === "card" && <CreateCardPage key={version} />}
        {page === "location" && <CreateLocationPage key={version} />}
      </main>
    </div>
  );
}
