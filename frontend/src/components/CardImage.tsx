import { useState } from "react";
import type { Card } from "../types/catalogs";
import { API_BASE_URL } from "../services/api";

type Props = { card: Pick<Card, "name" | "imageUrl" | "imageSmallUrl">; small?: boolean; className?: string };

// La miniatura usa su archivo generado; nunca lo sustituye por el icono del tipo.
export default function CardImage({ card, small = false, className }: Props) {
  const path = small ? card.imageSmallUrl : card.imageUrl;
  const src = path ? new URL(path, `${API_BASE_URL}/`).href : null;
  const [failedUrl, setFailedUrl] = useState<string | null>(null);
  if (!src || failedUrl === src)
    return <span className="card-image-missing" role="img" aria-label={`Imagen de ${card.name} no disponible`}>Imagen no disponible</span>;
  return <img className={className} src={src} alt={`Carta ${card.name}`} loading="lazy"
    onError={() => setFailedUrl(src)} />;
}
