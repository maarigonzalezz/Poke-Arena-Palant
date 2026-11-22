import { lazy, Suspense } from "react";

// Cada entrada carga sus módulos sin incluir el menú administrativo en el ingreso.
const LoginPage = lazy(() => import("./pages/LoginPage"));
const AdminPage = lazy(() => import("./App"));

export default function RootPage() {
  const path = window.location.pathname.replace(/\/+$/, "") || "/";
  if (path === "/" || path === "/login") {
    return <Suspense fallback={<p>Cargando...</p>}><LoginPage /></Suspense>;
  }
  if (path === "/admin") {
    return <Suspense fallback={<p>Cargando...</p>}><AdminPage /></Suspense>;
  }
  return <main><h1>Página no encontrada</h1><a href="/">Ir al inicio de sesión</a></main>;
}
