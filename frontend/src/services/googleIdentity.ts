interface GoogleCredential { credential: string }

export interface GoogleIdentity {
  initialize(options: { client_id: string; callback: (response: GoogleCredential) => void; auto_select: boolean }): void;
  renderButton(parent: HTMLElement, options: { type: "standard"; text: "signin_with" }): void;
  disableAutoSelect(): void;
}

let loading: Promise<GoogleIdentity> | null = null;
const identity = () => (window as Window & { google?: { accounts: { id: GoogleIdentity } } }).google?.accounts.id;

// Carga únicamente el SDK oficial; no existe un proveedor alterno o simulado.
export function loadGoogleIdentity(): Promise<GoogleIdentity> {
  const existing = identity();
  if (existing) return Promise.resolve(existing);
  if (loading) return loading;
  loading = new Promise((resolve, reject) => {
    const script = document.createElement("script");
    script.src = "https://accounts.google.com/gsi/client";
    script.async = true;
    script.onload = () => {
      const sdk = identity();
      if (sdk) resolve(sdk);
      else { loading = null; script.remove(); reject(new Error("No se pudo inicializar Google.")); }
    };
    script.onerror = () => {
      loading = null;
      script.remove();
      reject(new Error("No se pudo cargar Google. Revise su conexión."));
    };
    document.head.appendChild(script);
  });
  return loading;
}
