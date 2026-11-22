
import React from "react";
import ReactDOM from "react-dom/client";

import "./index.css";
import RootPage from "./RootPage";

// Monta la aplicación y activa las comprobaciones de React en desarrollo.
ReactDOM.createRoot(
  document.getElementById("root")!
).render(
  <React.StrictMode>
    <RootPage />
  </React.StrictMode>
);
