import { IMAGE_RULES, readImage, validateImage, type ImageKind } from "./imageValidation";

// Redimensiona la imagen completa, incluso si es pequeña o tiene otra proporción.
export async function resizeImage(file: File, kind: ImageKind) {
  const original = await readImage(file);
  try {
    const image = new Image();
    image.src = original.previewUrl;
    await image.decode();
    const { width, height } = IMAGE_RULES[kind];
    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d");
    if (!context) throw new Error("No se pudo preparar la imagen en este navegador.");
    context.imageSmoothingQuality = "high";
    // Ajusta ambos ejes al tamaño solicitado sin recortar ninguna parte de la imagen.
    context.drawImage(image, 0, 0, width, height);
    const blob = await new Promise<Blob>((resolve, reject) => {
      canvas.toBlob(value => value ? resolve(value) : reject(new Error("No se pudo redimensionar la imagen.")), "image/png");
    });
    const resized = new File([blob], `${kind}-original.png`, { type: "image/png" });
    const info = await validateImage(resized, kind);
    return { file: resized, info };
  } finally {
    URL.revokeObjectURL(original.previewUrl);
  }
}
