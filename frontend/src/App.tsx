
import { useEffect, useState } from "react";
import { checkBackend } from "./services/api";

function App() {
  const [status, setStatus] = useState(
    "Comprobando conexión..."
  );

  useEffect(() => {
    checkBackend()
      .then((data) => {
        setStatus(data.message);
      })
      .catch(() => {
        setStatus("Error al conectar con el backend");
      });
  }, []);

  return (
    <main
      style={{
        maxWidth: "800px",
        margin: "60px auto",
        fontFamily: "Arial, sans-serif",
        padding: "24px",
      }}
    >
      <h1>Poké-Arena</h1>

      <h2>Sprint 1</h2>

      <p>Estado de la API:</p>

      <strong>{status}</strong>

      <hr />

      <h3>Funcionalidades asignadas</h3>

      <ul>
        <li>Buscar Carta</li>
        <li>Crear Carta — Interfaz gráfica</li>
        <li>Crear Ubicación — Interfaz gráfica</li>
      </ul>
    </main>
  );
}

export default App;
