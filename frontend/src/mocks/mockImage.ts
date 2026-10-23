import { validateImage, type ImageKind } from "../services/imageValidation";

/** Valida la imagen y devuelve una miniatura WebP como data URL para guardarla localmente. */
export async function createMockThumbnail(file: File, kind: ImageKind): Promise<string> {
  const info = await validateImage(file, kind);
  try {
    const image = new Image();
    image.src = info.previewUrl;
    await image.decode();
    const canvas = document.createElement("canvas");
    canvas.width = kind === "card" ? 200 : 320;
    canvas.height = kind === "card" ? 280 : 240;
    const context = canvas.getContext("2d");
    if (!context) throw new Error("No se pudo preparar la vista previa.");
    context.drawImage(image, 0, 0, canvas.width, canvas.height);
    return canvas.toDataURL("image/webp", 0.85);
  } finally {
    URL.revokeObjectURL(info.previewUrl);
  }
}
