
export type ImageKind = "card" | "location";

export interface ImageInfo {
  width: number;
  height: number;
  previewUrl: string;
}

const MAX_SIZE = 5 * 1024 * 1024;

// La conversión automática y la validación final comparten las dimensiones de salida.
export const IMAGE_RULES = {
  card: { width: 600, height: 840 },
  location: { width: 800, height: 600 },
} as const;

const ALLOWED_TYPES = [
  "image/png",
  "image/jpeg",
  "image/webp",
];

/** Abre una imagen local; el consumidor libera previewUrl cuando deja de usarla. */
export async function readImage(file: File): Promise<ImageInfo> {
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

  // Crea una referencia temporal al archivo sin subirlo todavía.
  const url = URL.createObjectURL(file);

  try {
    const image = new Image();
    image.src = url;
    // Espera la decodificación para leer las dimensiones reales.
    await image.decode();

    const width = image.naturalWidth;
    const height = image.naturalHeight;

    return { width, height, previewUrl: url };
  } catch (error) {
    URL.revokeObjectURL(url);
    throw error;
  }
}

/** Comprueba el archivo definitivo antes de enviarlo al backend. */
export async function validateImage(file: File, kind: ImageKind): Promise<ImageInfo> {
  const info = await readImage(file);
  try {
    const { width, height } = info;

    // Cartas y ubicaciones tienen resoluciones y proporciones distintas.
    const { width: minWidth, height: minHeight } = IMAGE_RULES[kind];

    if (width < minWidth || height < minHeight) {
      throw new Error(
        `La resolución mínima es ${minWidth} × ${minHeight} píxeles.`
      );
    }

    const expectedRatio = minWidth / minHeight;

    const ratio = width / height;

    // Tolera redondeos de píxeles, pero rechaza proporciones diferentes.
    if (Math.abs(ratio - expectedRatio) > 0.001) {
      throw new Error(
        kind === "card"
          ? "La carta necesita proporción vertical 5:7."
          : "La ubicación necesita proporción horizontal 4:3."
      );
    }

    return info;
  // Libera la referencia temporal cuando la imagen no supera la validación.
  } catch (error) {
    URL.revokeObjectURL(info.previewUrl);
    throw error;
  }
}
