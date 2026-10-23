
import type {
  Ability,
  Card,
  CardFilters,
  CardSearchResult,
  CreateCardInput,
  CreateLocationInput,
} from "../types/catalogs";
import { CARD_TYPES, RARITIES, EFFECT_TYPES } from "../types/catalogs";
import { createMockThumbnail } from "./mockImage";
import {
  MOCK_ABILITIES,
  buildMockCards,
} from "./catalogData";

// Simula el catálogo en el navegador: registros persistentes y sesiones temporales.
type MockLocation = {
  id: number;
  name: string;
  description: string;
  maxCards: number;
  energyMode: string;
  extraEnergy: number;
  effectType: string;
  effectParameters: Record<string, unknown>;
  imageUrl: string;
};

type SearchSession = {
  codes: string[];
  snapshot: Card[];
  createdAt: number;
};

const CARDS_KEY = "pokearena_mock_cards_v1";
const LOCATIONS_KEY = "pokearena_mock_locations_v1";

// Las sesiones y las claves de reintento se pierden al recargar; los registros no.
const sessions = new Map<string, SearchSession>();

const completedRequests = new Map<
  string,
  { fingerprint: string; response: unknown }
>();

const SEARCH_EXPIRATION = 30 * 60 * 1000;
let pendingWrites: Promise<unknown> = Promise.resolve();

// Ordena las escrituras de esta instancia para evitar duplicados en envíos simultáneos.
function serialize<T>(operation: () => Promise<T>): Promise<T> {
  const result = pendingWrites.then(operation);
  pendingWrites = result.catch(() => undefined);
  return result;
}

function delay(ms = 250): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// Recupera el catálogo inicial cuando no hay datos locales válidos.
function readCards(): Card[] {
  const stored = localStorage.getItem(CARDS_KEY);

  if (stored !== null) {
    try {
      const cards: unknown = JSON.parse(stored);
      if (Array.isArray(cards) && cards.every(isStoredCard)) return cards;
    } catch {
      // Recuperar el catálogo de prueba.
    }
  }

  const initialCards = buildMockCards();
  localStorage.setItem(CARDS_KEY, JSON.stringify(initialCards));

  return initialCards;
}

function writeCards(cards: Card[]) {
  try {
    localStorage.setItem(CARDS_KEY, JSON.stringify(cards));
  } catch {
    throw new Error("No hay espacio para guardar la simulación en este navegador. Restablezca los datos de prueba.");
  }
}

function isStoredCard(value: unknown): value is Card {
  if (!value || typeof value !== "object") return false;
  const card = value as Record<string, unknown>;
  return ["code", "name", "type", "rarity", "abilityCode", "abilityName", "imageStatus"]
    .every(key => typeof card[key] === "string") &&
    typeof card.cost === "number" && typeof card.power === "number";
}

// Permite buscar nombres sin distinguir mayúsculas ni tildes.
function normalize(value: string): string {
  return value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLocaleLowerCase("es");
}

function matches(card: Card, filters: CardFilters): boolean {
  if (filters.code && card.code !== filters.code.trim())
    return false;

  if (
    filters.name &&
    !normalize(card.name).includes(normalize(filters.name.trim()))
  ) {
    return false;
  }

  if (filters.cost !== undefined && card.cost !== filters.cost)
    return false;

  if (
    filters.minCost !== undefined &&
    card.cost < filters.minCost
  ) return false;

  if (
    filters.maxCost !== undefined &&
    card.cost > filters.maxCost
  ) return false;

  if (
    filters.power !== undefined &&
    card.power !== filters.power
  ) return false;

  if (
    filters.minPower !== undefined &&
    card.power < filters.minPower
  ) return false;

  if (
    filters.maxPower !== undefined &&
    card.power > filters.maxPower
  ) return false;

  if (filters.type && card.type !== filters.type)
    return false;

  if (filters.rarity && card.rarity !== filters.rarity)
    return false;

  if (
    filters.abilityCode &&
    card.abilityCode !== filters.abilityCode.trim()
  ) return false;

  if (
    filters.abilityTrigger &&
    card.abilityTrigger !== filters.abilityTrigger.trim()
  ) return false;

  if (
    filters.abilityEffect &&
    card.abilityEffect !== filters.abilityEffect.trim()
  ) return false;

  return true;
}

/** Conserva solo las cartas que cumplen todos los filtros recibidos. */
export function filterMockCards(
  cards: Card[],
  filters: CardFilters
): Card[] {
  return cards.filter((card) => matches(card, filters));
}

function validateSearch(filters: CardFilters) {
  const after = filters.after ?? 0;
  if (!Number.isSafeInteger(after) || after < 0)
    throw new Error("Posición de página inválida.");
  if (!filters.searchId && after !== 0)
    throw new Error("Se requiere una sesión de búsqueda para continuar.");
  if (filters.searchId && Object.entries(filters).some(([key, value]) =>
    !["searchId", "after", "pageSize"].includes(key) && value !== undefined))
    throw new Error("No cambie los filtros al continuar una búsqueda.");
  if (filters.type !== undefined && !CARD_TYPES.includes(filters.type))
    throw new Error("Tipo no permitido.");
  if (filters.rarity !== undefined && !RARITIES.includes(filters.rarity))
    throw new Error("Rareza no permitida.");
  const pageSize = filters.pageSize ?? 20;

  if (
    !Number.isInteger(pageSize) ||
    pageSize < 1 ||
    pageSize > 100
  ) {
    throw new Error("pageSize debe estar entre 1 y 100.");
  }

  const numericFields = [
    ["cost", filters.cost, 0, 10],
    ["minCost", filters.minCost, 0, 10],
    ["maxCost", filters.maxCost, 0, 10],
    ["power", filters.power, -20, 20],
    ["minPower", filters.minPower, -20, 20],
    ["maxPower", filters.maxPower, -20, 20],
  ] as const;

  for (const [name, value, min, max] of numericFields) {
    if (
      value !== undefined &&
      (!Number.isInteger(value) || value < min || value > max)
    ) {
      throw new Error(`${name}: rango inválido.`);
    }
  }

  if (
    filters.cost !== undefined &&
    (filters.minCost !== undefined ||
      filters.maxCost !== undefined)
  ) {
    throw new Error("No combine costo exacto con rango.");
  }

  if (
    filters.power !== undefined &&
    (filters.minPower !== undefined ||
      filters.maxPower !== undefined)
  ) {
    throw new Error("No combine poder exacto con rango.");
  }

  if (
    filters.minCost !== undefined &&
    filters.maxCost !== undefined &&
    filters.minCost > filters.maxCost
  ) {
    throw new Error("Rango de costo inválido.");
  }

  if (
    filters.minPower !== undefined &&
    filters.maxPower !== undefined &&
    filters.minPower > filters.maxPower
  ) {
    throw new Error("Rango de poder inválido.");
  }
}

/** Pagina una copia fija de los resultados durante 30 minutos, sin acceder al backend. */
export async function mockSearchCards(
  filters: CardFilters
): Promise<CardSearchResult> {
  await delay();
  validateSearch(filters);
  for (const [id, session] of sessions) {
    if (Date.now() - session.createdAt >= SEARCH_EXPIRATION) sessions.delete(id);
  }

  const pageSize = filters.pageSize ?? 20;
  const after = filters.after ?? 0;

  if (!Number.isInteger(after) || after < 0) {
    throw new Error("Posición de página inválida.");
  }

  let searchId = filters.searchId;
  let session: SearchSession | undefined;

  if (searchId) {
    session = sessions.get(searchId);

    if (
      !session ||
      Date.now() - session.createdAt >= SEARCH_EXPIRATION
    ) {
      sessions.delete(searchId);
      throw new Error(
        "La búsqueda ha expirado. Vuelva a realizarla."
      );
    }
  } else {
    searchId = crypto.randomUUID();

    const filtered = filterMockCards(
      readCards(),
      filters
    ).sort((a, b) => a.code.localeCompare(b.code));

    session = {
      codes: filtered.map((card) => card.code),
      snapshot: structuredClone(filtered),
      createdAt: Date.now(),
    };

    sessions.set(searchId, session);
  }

  const page = session.snapshot.slice(after, after + pageSize);
  const next =
    after + page.length < session.codes.length
      ? after + page.length
      : null;

  return {
    items: structuredClone(page),
    searchId,
    nextPosition: next,
    totalCount: session.codes.length,
  };
}

/** Devuelve una copia del catálogo para evitar que el consumidor modifique los datos base. */
export async function mockGetAbilities(): Promise<Ability[]> {
  await delay();
  return structuredClone(MOCK_ABILITIES);
}

// La misma clave y contenido repiten la respuesta; cambiar el contenido produce un error.
function checkIdempotency(
  key: string,
  fingerprint: string
): unknown | undefined {
  const previous = completedRequests.get(key);

  if (!previous) return undefined;

  if (previous.fingerprint !== fingerprint) {
    throw new Error(
      "La clave de idempotencia ya fue utilizada con otros datos."
    );
  }

  return previous.response;
}

/** Guarda una carta y su miniatura en localStorage con protección ante reintentos. */
export function mockCreateCard(input: CreateCardInput, idempotencyKey: string): Promise<unknown> {
  return serialize(() => createCard(input, idempotencyKey));
}

async function createCard(
  input: CreateCardInput,
  idempotencyKey: string
): Promise<unknown> {
  await delay(400);

  if (!input.name.trim()) throw new Error("El nombre es obligatorio.");
  if (!CARD_TYPES.includes(input.type) || !RARITIES.includes(input.rarity))
    throw new Error("Tipo o rareza no permitidos.");
  if (!idempotencyKey.trim()) throw new Error("Falta la clave de idempotencia.");
  const requestKey = `card:${idempotencyKey}`;

  const fingerprint = JSON.stringify({
    name: input.name,
    cost: input.cost,
    power: input.power,
    type: input.type,
    rarity: input.rarity,
    abilityCode: input.abilityCode,
    fileName: input.image.name,
    fileSize: input.image.size,
    fileHash: await fileHash(input.image),
  });

  const replay = checkIdempotency(
    requestKey,
    fingerprint
  );

  if (replay !== undefined) return replay;

  const ability = MOCK_ABILITIES.find(
    (a) => a.code === input.abilityCode
  );

  if (!ability) {
    throw new Error("La habilidad no existe.");
  }

  if (
    ability.restrictedCardType &&
    ability.restrictedCardType !== input.type
  ) {
    throw new Error(
      "La habilidad no corresponde al tipo de carta."
    );
  }

  if (
    input.cost < 0 || input.cost > 10 ||
    !Number.isInteger(input.cost) ||
    input.power < -20 || input.power > 20 ||
    !Number.isInteger(input.power)
  ) {
    throw new Error("Costo o poder inválido.");
  }

  const cards = readCards();

  const prefixes: Record<Card["type"], string> = {
    Fuego: "FUE",
    Agua: "AGU",
    Planta: "PLA",
    Eléctrico: "ELE",
    Psíquico: "PSI",
    Lucha: "LUC",
  };

  const prefix = `PKA-${prefixes[input.type]}-`;

  const nextNumber = Math.max(
    0,
    ...cards
      .filter((card) => card.code.startsWith(prefix))
      .map((card) => Number(card.code.slice(prefix.length)))
      .filter(Number.isFinite)
  ) + 1;

  const code = `${prefix}${String(nextNumber).padStart(3, "0")}`;

  const card: Card = {
    code,
    name: input.name.trim(),
    cost: input.cost,
    power: input.power,
    type: input.type,
    rarity: input.rarity,
    abilityCode: ability.code,
    abilityName: ability.name,
    abilityTrigger: ability.trigger,
    abilityEffect: ability.effect,
    abilityParameters: ability.parameters,
    imageStatus: "Ready",
    imageUrl: await createMockThumbnail(input.image, "card"),
  };

  writeCards([...cards, card]);

  const response = {
    code,
    status: "CreatedMock",
    message: "Carta creada únicamente en la simulación.",
  };

  completedRequests.set(requestKey, {
    fingerprint,
    response,
  });

  return response;
}

/** Guarda una ubicación local con validación de energía, efectos e imagen. */
export function mockCreateLocation(input: CreateLocationInput, idempotencyKey: string): Promise<unknown> {
  return serialize(() => createLocation(input, idempotencyKey));
}

async function createLocation(
  input: CreateLocationInput,
  idempotencyKey: string
): Promise<unknown> {
  await delay(400);

  if (!input.name.trim() || !input.description.trim())
    throw new Error("El nombre y la descripción son obligatorios.");
  if (!["Fixed", "PerCard"].includes(input.energyMode))
    throw new Error("Modalidad de energía inválida.");
  if (!EFFECT_TYPES.includes(input.effectType))
    throw new Error("Efecto no permitido.");
  if (!idempotencyKey.trim()) throw new Error("Falta la clave de idempotencia.");
  const requestKey = `location:${idempotencyKey}`;
  validateEffect(input);

  const fingerprint = JSON.stringify({
    ...input,
    image: {
      name: input.image.name,
      size: input.image.size,
      hash: await fileHash(input.image),
    },
  });

  const replay = checkIdempotency(
    requestKey,
    fingerprint
  );

  if (replay !== undefined) return replay;

  if (
    !Number.isInteger(input.maxCards) ||
    input.maxCards < 0 ||
    input.maxCards > 5
  ) {
    throw new Error("Límite de cartas inválido.");
  }

  const maximum = input.energyMode === "Fixed" ? 3 : 2;

  if (
    !Number.isInteger(input.extraEnergy) ||
    input.extraEnergy < 0 ||
    input.extraEnergy > maximum
  ) {
    throw new Error("Energía adicional inválida.");
  }

  const stored = localStorage.getItem(LOCATIONS_KEY);
  let locations: MockLocation[] = [];

  if (stored) {
    try {
      const parsed: unknown = JSON.parse(stored);
      if (Array.isArray(parsed) && parsed.every(item => item && Number.isSafeInteger(item.id)))
        locations = parsed as MockLocation[];
    } catch {
      locations = [];
    }
  }

  const id = Math.max(0, ...locations.map((l) => l.id)) + 1;

  const location: MockLocation = {
    id,
    name: input.name.trim(),
    description: input.description.trim(),
    maxCards: input.maxCards,
    energyMode: input.energyMode,
    extraEnergy: input.extraEnergy,
    effectType: input.effectType,
    effectParameters: input.effectParameters,
    imageUrl: await createMockThumbnail(input.image, "location"),
  };

  localStorage.setItem(
    LOCATIONS_KEY,
    JSON.stringify([...locations, location])
  );

  const response = {
    id,
    status: "CreatedMock",
    message: "Ubicación creada únicamente en la simulación.",
  };

  completedRequests.set(requestKey, {
    fingerprint,
    response,
  });

  return response;
}

/** Espera los guardados pendientes y elimina únicamente el estado de esta simulación. */
export async function resetMockCatalog(): Promise<void> {
  await pendingWrites;
  localStorage.removeItem(CARDS_KEY);
  localStorage.removeItem(LOCATIONS_KEY);
  sessions.clear();
  completedRequests.clear();
}

// Compara el contenido de la imagen, incluso si dos archivos tienen igual nombre y tamaño.
async function fileHash(file: File): Promise<string> {
  if (!file || !["image/png", "image/jpeg", "image/webp"].includes(file.type) ||
      file.size === 0 || file.size > 5 * 1024 * 1024)
    throw new Error("Seleccione una imagen PNG, JPG o WebP válida de hasta 5 MB.");
  const digest = await crypto.subtle.digest("SHA-256", await file.arrayBuffer());
  return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, "0")).join("");
}

function validateEffect(input: CreateLocationInput) {
  const params = input.effectParameters;
  if (input.effectType === "TypeBonus" || input.effectType === "CostModifier") {
    const amount = input.effectType === "TypeBonus" ? params.power : params.amount;
    if (typeof amount !== "number" || !Number.isInteger(amount) || amount < -5 || amount > 5)
      throw new Error("El modificador debe estar entre -5 y +5.");
    const type = input.effectType === "TypeBonus" ? params.type : params.affectedType;
    if (!(input.effectType === "CostModifier" && type === null) && !CARD_TYPES.some(item => item === type))
      throw new Error("Tipo afectado no permitido.");
  }
  if (input.effectType === "TurnRestriction") {
    const turns = params.turns;
    if (!Array.isArray(turns) || turns.length === 0 ||
        turns.some(turn => !Number.isInteger(turn) || turn < 1 || turn > 6) ||
        new Set(turns).size !== turns.length)
      throw new Error("Seleccione turnos válidos del 1 al 6, sin repetir.");
  }
}
