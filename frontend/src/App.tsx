
import { useEffect, useState } from "react";
import { checkBackend } from "./services/api";
import "./App.css";

type ConnectionStatus =
  | "loading"
  | "connected"
  | "error";

function App() {
  const [status, setStatus] =
    useState<ConnectionStatus>("loading");

  const [message, setMessage] = useState<string>(
    "Comprobando conexión con el backend..."
  );

  useEffect(() => {
    async function verifyConnection() {
      try {
        const response = await checkBackend();

        setStatus("connected");
        setMessage(response.message);
      } catch (error) {
        console.error("Error de conexión:", error);

        setStatus("error");
        setMessage(
          "No se pudo establecer conexión con el backend."
        );
      }
    }

    void verifyConnection();
  }, []);

  return (
    <div className="app-container">
      <header className="app-header">
        <h1>Poké-Arena</h1>
        <p>Sistema de cartas coleccionables</p>
      </header>

      <main className="app-main">
        <section className="status-card">
          <h2>Estado del sistema</h2>

          <div className={`connection-status ${status}`}>
            {message}
          </div>
        </section>

        <section className="features-card">
          <h2>Funcionalidades del Sprint 1</h2>

          <div className="features-grid">
            <article className="feature">
              <h3>Buscar Carta</h3>
              <p>
                Consultar cartas mediante filtros
                de búsqueda combinados.
              </p>
              <span>F-02</span>
            </article>

            <article className="feature">
              <h3>Crear Carta</h3>
              <p>
                Interfaz para registrar nuevas
                cartas en el catálogo.
              </p>
              <span>F-02</span>
            </article>

            <article className="feature">
              <h3>Crear Ubicación</h3>
              <p>
                Interfaz para configurar nuevas
                ubicaciones de juego.
              </p>
              <span>F-03</span>
            </article>
          </div>
        </section>
      </main>

      <footer className="app-footer">
        Poké-Arena — Sprint 1
      </footer>
    </div>
  );
}

export default App;
