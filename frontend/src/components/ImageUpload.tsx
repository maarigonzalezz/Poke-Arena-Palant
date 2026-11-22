import { useEffect, useRef, useState } from "react";
import { IMAGE_RULES, type ImageInfo, type ImageKind } from "../services/imageValidation";
import { resizeImage } from "../services/imageResize";
import "./ImageUpload.css";

type Props = { kind: ImageKind; disabled?: boolean; onChange: (file: File | null) => void };

// Seleccionar un archivo prepara automáticamente el original que recibirá el backend.
export default function ImageUpload({ kind, disabled = false, onChange }: Props) {
  const [preview, setPreview] = useState<(ImageInfo & { name: string }) | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const generation = useRef(0);
  const target = IMAGE_RULES[kind];

  // Descarta conversiones tardías y libera la vista previa al reemplazarla o salir.
  useEffect(() => () => { generation.current++; }, []);
  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview.previewUrl); }, [preview]);

  async function choose(file?: File) {
    if (!file) return;
    const version = ++generation.current;
    onChange(null);
    setPreview(null);
    setError("");
    setBusy(true);
    try {
      const result = await resizeImage(file, kind);
      if (version !== generation.current) {
        URL.revokeObjectURL(result.info.previewUrl);
        return;
      }
      setPreview({ ...result.info, name: file.name });
      onChange(result.file);
    } catch (cause) {
      if (version === generation.current) setError(cause instanceof Error ? cause.message : "No se pudo preparar la imagen.");
    } finally {
      if (version === generation.current) setBusy(false);
    }
  }

  return (
    <fieldset className="image-upload" disabled={disabled || busy}>
      <legend>Subir imagen</legend>
      <label>
        Imagen de {kind === "card" ? "la carta" : "la ubicación"}
        <input type="file" accept="image/png,image/jpeg,image/webp" onChange={event => {
          void choose(event.target.files?.[0]); event.target.value = "";
        }} />
      </label>
      <p className="help-text">PNG, JPG o WebP, máximo 5 MB. Cualquier resolución o proporción se ajustará automáticamente a {target.width} × {target.height} px.</p>
      {busy && <p role="status">Redimensionando imagen…</p>}
      {preview && <>
        <p className="help-text">{preview.name}</p>
        <img src={preview.previewUrl} width={preview.width} height={preview.height} alt="Vista previa de la imagen redimensionada" />
        <p role="status">Imagen lista: {preview.width} × {preview.height} px. El servidor generará también su miniatura.</p>
      </>}
      {error && <p className="alert error" role="alert">{error}</p>}
    </fieldset>
  );
}
