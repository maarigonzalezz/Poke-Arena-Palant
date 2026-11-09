import { useCallback, useEffect, useRef, useState } from "react";
import { getLoginConfiguration, loginWithGoogle, type LoginConfiguration, type LoginResult } from "../services/authApi";
import { loadGoogleIdentity, type GoogleIdentity } from "../services/googleIdentity";
import PlayerExperiencePage from "./PlayerExperiencePage";
import "../App.css";
import { setPlayerAccessToken, signOutPlayer } from "../services/playerApi";

/** Autentica con Google contra la API y continúa a la experiencia del jugador. */
export default function LoginPage() {
  const [configuration, setConfiguration] = useState<LoginConfiguration | null>(null);
  const [session, setSession] = useState<LoginResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [status, setStatus] = useState("");
  const [googleReady, setGoogleReady] = useState(false);
  const buttonRef = useRef<HTMLDivElement>(null);
  const sdkRef = useRef<GoogleIdentity | null>(null);
  const callbackRef = useRef<(credential: string) => void>(() => undefined);
  const requestRef = useRef<AbortController | null>(null);
  const isAuthenticated = session !== null;

  // La sesión vive en memoria; recargar la página requiere volver a ingresar.
  const exchange = useCallback(async (credential: string) => {
    if (requestRef.current) return;
    const controller = new AbortController();
    requestRef.current = controller;
    setBusy(true); setError(""); setStatus("Google confirmó la cuenta. Validando el acceso...");
    try {
      const result = await loginWithGoogle(credential, controller.signal);
      if (controller.signal.aborted) return;
      setPlayerAccessToken(result.accessToken);
      setSession(result);
    } catch (cause) {
      if (!controller.signal.aborted)
        setError(cause instanceof Error ? cause.message : "No se pudo iniciar sesión.");
    } finally {
      if (requestRef.current === controller) requestRef.current = null;
      if (!controller.signal.aborted) {
        setBusy(false);
        setStatus("");
      }
    }
  }, []);

  useEffect(() => {
    callbackRef.current = credential => { void exchange(credential); };
  }, [exchange]);

  useEffect(() => {
    const controller = new AbortController();
    getLoginConfiguration(controller.signal).then(setConfiguration).catch((cause: unknown) => {
      if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : "No se pudo consultar la API.");
    });
    return () => { controller.abort(); requestRef.current?.abort(); requestRef.current = null; };
  }, []);

  useEffect(() => {
    if (!configuration?.enabled || !configuration.googleClientId || sdkRef.current) return;
    let active = true;
    const clientId = configuration.googleClientId;
    loadGoogleIdentity().then(sdk => {
      if (!active) return;
      sdkRef.current = sdk;
      sdk.initialize({ client_id: clientId, auto_select: false,
        callback: response => { if (active) callbackRef.current(response.credential); } });
      setGoogleReady(true);
    }).catch((cause: unknown) => {
      if (active) setError(cause instanceof Error ? cause.message : "No se pudo cargar Google.");
    });
    return () => { active = false; };
  }, [configuration]);

  useEffect(() => {
    if (isAuthenticated || !googleReady || !sdkRef.current || !buttonRef.current) return;
    const container = buttonRef.current;
    container.replaceChildren();
    sdkRef.current.renderButton(container, {
      type: "standard", text: "signin_with", theme: "outline",
      size: "large", shape: "rectangular", width: 320,
    });
    return () => { container.replaceChildren(); };
  }, [googleReady, isAuthenticated]);

  useEffect(() => {
    if (!session) return;
    const timer = window.setTimeout(() => {
      signOutPlayer();
      setSession(null); setError("La sesión venció. Inicie sesión nuevamente.");
    }, Math.max(0, Date.parse(session.expiresAt) - Date.now()));
    return () => window.clearTimeout(timer);
  }, [session]);

  if (session) {
    return (
      <PlayerExperiencePage
        key="authenticated"
        initialStage="welcome"
        remoteData
        cubeBalance={session.player.cubes}
        playerName={session.player.displayName ?? "Jugador"}
        onOpenAdmin={() => window.location.assign("/admin")}
        onSignOut={() => {
          signOutPlayer();
          setSession(null);
          setError("");
          setStatus("");
          sdkRef.current?.disableAutoSelect();
        }}
      />
    );
  }

  return (
    <PlayerExperiencePage
      key="anonymous"
      onOpenAdmin={() => window.location.assign("/admin")}
      loginButton={<div ref={buttonRef} className={`google-sign-in-slot${busy ? " is-disabled" : ""}`} />}
      loginError={error}
      loginStatus={status || (busy
        ? "Conectando con Google..."
        : !configuration
          ? "Conectando con la API..."
          : !configuration.enabled
            ? "El inicio de sesión no está configurado. Revisa la configuración de Google y la firma del backend."
            : "")}
    />
  );
}
