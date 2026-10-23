
import React from "react";
import ReactDOM from "react-dom/client";

import App from "./App";
import "./index.css";

// Monta la aplicación y activa las comprobaciones de React en desarrollo.
ReactDOM.createRoot(
  document.getElementById("root")!
).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
