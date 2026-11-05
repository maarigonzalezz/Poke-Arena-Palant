import { useCallback, useEffect, useRef, useState } from "react";
import { getCurrentPlayer, getLoginConfiguration, loginWithGoogle,
  type LoginConfiguration, type LoginResult } from "../services/authApi";
import { loadGoogleIdentity, type GoogleIdentity } from "../services/googleIdentity";

/** Pantalla mínima para ingresar con Google y comprobar las credenciales del backend real. */
export default function LoginPage() {
  const [configuration, setConfiguration] = useState<LoginConfiguration | null>(null);
  const [idToken, setIdToken] = useState("");
  const [session, setSession] = useState<LoginResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const buttonRef = useRef<HTMLDivElement>(null);
  const sdkRef = useRef<GoogleIdentity | null>(null);
  const requestRef = useRef<AbortController | null>(null);

  // La sesión vive en memoria; recargar la página requiere volver a ingresar.
  const exchange = useCallback(async (credential: string) => {
    if (requestRef.current) return;
    const controller = new AbortController();
    requestRef.current = controller;
    setBusy(true); setError(""); setMessage("");
    try {
      const result = await loginWithGoogle(credential, controller.signal);
      setIdToken("");
      setSession(result);
      setMessage(result.isNewPlayer ? "Cuenta creada. Sesión iniciada." : "Sesión iniciada.");
    } catch (cause) {
      if (!controller.signal.aborted)
        setError(cause instanceof Error ? cause.message : "No se pudo iniciar sesión.");
    } finally {
      if (requestRef.current === controller) requestRef.current = null;
      if (!controller.signal.aborted) setBusy(false);
    }
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    getLoginConfiguration(controller.signal).then(setConfiguration).catch((cause: unknown) => {
      if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : "No se pudo consultar la API.");
    });
    return () => { controller.abort(); requestRef.current?.abort(); requestRef.current = null; };
  }, []);

  useEffect(() => {
    if (!configuration?.enabled || !configuration.googleClientId) return;
    let active = true;
    const clientId = configuration.googleClientId;
    const container = buttonRef.current;
    loadGoogleIdentity().then(sdk => {
      if (!active || !container) return;
      sdkRef.current = sdk;
      sdk.initialize({ client_id: clientId, auto_select: false,
        callback: response => { if (active) void exchange(response.credential); } });
      container.replaceChildren();
      sdk.renderButton(container, { type: "standard", text: "signin_with" });
    }).catch((cause: unknown) => {
      if (active) setError(cause instanceof Error ? cause.message : "No se pudo cargar Google.");
    });
    return () => { active = false; container?.replaceChildren(); };
  }, [configuration, exchange]);

  useEffect(() => {
    if (!session) return;
    const timer = window.setTimeout(() => {
      setSession(null); setMessage(""); setError("La sesión venció. Inicie sesión nuevamente.");
    }, Math.max(0, Date.parse(session.expiresAt) - Date.now()));
    return () => window.clearTimeout(timer);
  }, [session]);

  async function verifySession() {
    if (!session || requestRef.current) return;
    const controller = new AbortController();
    requestRef.current = controller;
    setBusy(true); setError("");
    try {
      const player = await getCurrentPlayer(session.accessToken, controller.signal);
      setSession(current => current ? { ...current, player } : null);
      setMessage("La API confirmó la sesión y los datos del jugador.");
    } catch (cause) {
      if (!controller.signal.aborted) {
        setSession(null);
        setError(cause instanceof Error ? cause.message : "No se pudo verificar la sesión.");
      }
    } finally {
      if (requestRef.current === controller) requestRef.current = null;
      if (!controller.signal.aborted) setBusy(false);
    }
  }

  return (
    <main>
      <h1>Inicio de sesión</h1>
      <p>Ingrese con Google para acceder a Poké-Arena.</p>
      <label>Client ID de Google configurado en el servidor
        <input readOnly value={configuration?.googleClientId ?? ""} placeholder="Pendiente de configurar" />
      </label>
      {configuration && !configuration.enabled && (
        <p>Configure el Client ID de Google y la firma de tokens en el backend, reinicie la API y recargue esta página.</p>
      )}
      {!configuration && !error && <p>Cargando configuración...</p>}
      <div ref={buttonRef} hidden={!!session || busy} />
      {!session && (
        <details>
          <summary>Probar con un ID token real de Google</summary>
          <form onSubmit={event => { event.preventDefault(); if (idToken.trim()) void exchange(idToken.trim()); }}>
            <label>ID token de Google
              <input type="password" autoComplete="off" value={idToken} maxLength={10000}
                disabled={busy} onChange={event => setIdToken(event.target.value)} />
            </label>
            <button disabled={busy || !configuration?.enabled || !idToken.trim()} type="submit">Iniciar sesión</button>
          </form>
        </details>
      )}
      {busy && <p role="status">Procesando...</p>}
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
      {session && (
        <section>
          <h2>Cuenta del jugador</h2>
          <dl>
            <dt>Identificador</dt><dd>{session.player.id}</dd>
            <dt>Nombre</dt><dd>{session.player.displayName ?? "Sin nombre"}</dd>
            <dt>Rol</dt><dd>{session.player.role}</dd>
            <dt>Elo</dt><dd>{session.player.elo}</dd>
            <dt>Vencimiento</dt><dd>{new Date(session.expiresAt).toLocaleString()}</dd>
          </dl>
          <button type="button" disabled={busy} onClick={() => void verifySession()}>Comprobar sesión en la API</button>
          <button type="button" disabled={busy} onClick={() => {
            setSession(null); setIdToken(""); setError(""); setMessage("Sesión eliminada de este navegador.");
            sdkRef.current?.disableAutoSelect();
          }}>Cerrar sesión local</button>
          <details>
            <summary>Credencial propia para probar la API</summary>
            <label>Access token de Poké-Arena<input type="password" readOnly value={session.accessToken} autoComplete="off" /></label>
            <p>Se envía como Authorization: Bearer. No comparta esta credencial.</p>
          </details>
        </section>
      )}
    </main>
  );
}
