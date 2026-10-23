
export type ImageKind = "card" | "location";

export interface ImageInfo {
  width: number;
  height: number;
  previewUrl: string;
}

const MAX_SIZE = 5 * 1024 * 1024;

const ALLOWED_TYPES = [
  "image/png",
  "image/jpeg",
  "image/webp",
];

/** Valida formato, peso y dimensiones. El consumidor debe liberar previewUrl al dejar de usarla. */
export async function validateImage(
  file: File,
  kind: ImageKind
): Promise<ImageInfo> {
  if (!ALLOWED_TYPES.includes(file.type)) {
    throw new Error(
      "La imagen debe ser PNG, JPG o WebP."
    );
  }

  if (file.size > MAX_SIZE) {
    throw new Error(
      "La imagen no puede superar 5 MB."
    );
  }

  if (file.size === 0) {
    throw new Error("El archivo está vacío.");
  }

  const url = URL.createObjectURL(file);

  try {
    const image = new Image();
    image.src = url;
    await image.decode();

    const width = image.naturalWidth;
    const height = image.naturalHeight;

    const minWidth = kind === "card" ? 600 : 800;
    const minHeight = kind === "card" ? 840 : 600;

    if (width < minWidth || height < minHeight) {
      throw new Error(
        `La resolución mínima es ${minWidth} × ${minHeight} píxeles.`
      );
    }

    const expectedRatio =
      kind === "card" ? 5 / 7 : 4 / 3;

    const ratio = width / height;

    // La tolerancia solo absorbe redondeos de píxeles,
    // no admite imágenes de otra proporción.
    if (Math.abs(ratio - expectedRatio) > 0.001) {
      throw new Error(
        kind === "card"
          ? "La carta necesita proporción vertical 5:7."
          : "La ubicación necesita proporción horizontal 4:3."
      );
    }

    return {
      width,
      height,
      previewUrl: url,
    };
  } catch (error) {
    URL.revokeObjectURL(url);
    throw error;
  }
}
