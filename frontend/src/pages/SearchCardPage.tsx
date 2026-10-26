
import { useState } from "react";
import {
  CARD_TYPES,
  RARITIES,
  type Card,
  type CardFilters,
} from "../types/catalogs";
import { API_BASE_URL } from "../services/api";
import Icon from "../components/Icon";
import {
  searchCards,
} from "../services/catalogApi";

type FilterValues = {
  code: string;
  name: string;
  cost: string;
  minCost: string;
  maxCost: string;
  power: string;
  minPower: string;
  maxPower: string;
  type: string;
  rarity: string;
  abilityCode: string;
  abilityTrigger: string;
  abilityEffect: string;
};

const EMPTY: FilterValues = {
  code: "",
  name: "",
  cost: "",
  minCost: "",
  maxCost: "",
  power: "",
  minPower: "",
  maxPower: "",
  type: "",
  rarity: "",
  abilityCode: "",
  abilityTrigger: "",
  abilityEffect: "",
};

const PAGE_SIZE = 20;

// Un campo vacío omite el filtro; cero sigue siendo un criterio válido.
function asNumber(value: string): number | undefined {
  return value.trim() === "" ? undefined : Number(value);
}

function getFilters(values: FilterValues): CardFilters {
  return {
    code: values.code || undefined,
    name: values.name || undefined,
    cost: asNumber(values.cost),
    minCost: asNumber(values.minCost),
    maxCost: asNumber(values.maxCost),
    power: asNumber(values.power),
    minPower: asNumber(values.minPower),
    maxPower: asNumber(values.maxPower),
    type: values.type
      ? values.type as CardFilters["type"]
      : undefined,
    rarity: values.rarity
      ? values.rarity as CardFilters["rarity"]
      : undefined,
    abilityCode: values.abilityCode || undefined,
    abilityTrigger: values.abilityTrigger || undefined,
    abilityEffect: values.abilityEffect || undefined,
    pageSize: PAGE_SIZE,
  };
}

// Comprueba límites y evita mezclar valores exactos con rangos del mismo campo.
function validate(values: FilterValues): string | null {
  const fields = [
    ["Costo", values.cost, 0, 10],
    ["Costo mínimo", values.minCost, 0, 10],
    ["Costo máximo", values.maxCost, 0, 10],
    ["Poder", values.power, -20, 20],
    ["Poder mínimo", values.minPower, -20, 20],
    ["Poder máximo", values.maxPower, -20, 20],
  ] as const;

  for (const [name, raw, min, max] of fields) {
    if (raw.trim() === "") continue;
    const n = Number(raw);
    if (!Number.isInteger(n) || n < min || n > max) {
      return `${name}: ingrese un entero entre ${min} y ${max}.`;
    }
  }

  if (values.cost && (values.minCost || values.maxCost)) {
    return "Utilice costo exacto o rango, no ambos.";
  }

  if (values.power && (values.minPower || values.maxPower)) {
    return "Utilice poder exacto o rango, no ambos.";
  }

  if (
    values.minCost !== "" &&
    values.maxCost !== "" &&
    Number(values.minCost) > Number(values.maxCost)
  ) {
    return "El costo mínimo supera al máximo.";
  }

  if (
    values.minPower !== "" &&
    values.maxPower !== "" &&
    Number(values.minPower) > Number(values.maxPower)
  ) {
    return "El poder mínimo supera al máximo.";
  }

  return null;
}

// La API entrega parámetros como JSON textual y los mocks pueden devolver un objeto.
function formatParameters(
  value: Card["abilityParameters"]
): string {
  if (typeof value === "string") {
    try {
      return JSON.stringify(JSON.parse(value), null, 2);
    } catch {
      return value;
    }
  }
  return JSON.stringify(value ?? {}, null, 2);
}

/** Permite combinar filtros y cargar páginas de una misma sesión de búsqueda. */
export default function SearchCardPage() {
  const [filters, setFilters] = useState<FilterValues>(EMPTY);
  const [cards, setCards] = useState<Card[]>([]);
  const [searchId, setSearchId] = useState<string | null>(null);
  const [nextPosition, setNextPosition] = useState<number | null>(null);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [searched, setSearched] = useState(false);

  function update(field: keyof FilterValues, value: string) {
    setFilters((previous) => ({
      ...previous,
      [field]: value,
    }));
  }

  async function search() {
    const problem = validate(filters);
    if (problem) {
      setError(problem);
      return;
    }

    setLoading(true);
    setError("");
    setSearched(false);
    setCards([]);
    setSearchId(null);
    setNextPosition(null);

    try {
      const result = await searchCards(getFilters(filters));
      setCards(result.items);
      setSearchId(result.searchId);
      setNextPosition(result.nextPosition);
      setTotal(result.totalCount);
      setSearched(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error inesperado.");
    } finally {
      setLoading(false);
    }
  }

  // Continúa la sesión original; editar el formulario no altera sus resultados.
  async function loadMore() {
    if (!searchId || nextPosition === null || loading) return;

    setLoading(true);
    setError("");

    try {
      const result = await searchCards({
        searchId,
        after: nextPosition,
        pageSize: PAGE_SIZE,
      });

      setCards((previous) => [...previous, ...result.items]);
      setNextPosition(result.nextPosition);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Error al continuar.");
    } finally {
      setLoading(false);
    }
  }

  function clear() {
    setFilters({ ...EMPTY });
    setCards([]);
    setSearchId(null);
    setNextPosition(null);
    setTotal(0);
    setSearched(false);
    setError("");
  }

  return (
    <section className="page">
      <header className="page-heading">
        <div>
          <p className="eyebrow">Administración / Catálogo</p>
          <h1>Buscar carta</h1>
          <p>Encuentra la próxima protagonista de la arena. Combina criterios para explorar tu catálogo.</p>
        </div>
      </header>

      <form
        className="panel"
        onSubmit={(e) => {
          e.preventDefault();
          void search();
        }}
      >
        <div className="section-heading">
          <h2>Filtros de búsqueda</h2>
          <span>Todos los criterios se combinan</span>
        </div>

        <div className="form-grid search-basics">
          <label>
            Código
            <input
              value={filters.code}
              onChange={(e) => update("code", e.target.value)}
              placeholder="PKA-FUE-001"
            />
          </label>

          <label>
            Nombre
            <input
              value={filters.name}
              onChange={(e) => update("name", e.target.value)}
              placeholder="Nombre parcial"
            />
          </label>

          <label>
            Tipo
            <select
              value={filters.type}
              onChange={(e) => update("type", e.target.value)}
            >
              <option value="">Todos</option>
              {CARD_TYPES.map((t) => (
                <option key={t} value={t}>{t}</option>
              ))}
            </select>
          </label>

          <label>
            Rareza
            <select
              value={filters.rarity}
              onChange={(e) => update("rarity", e.target.value)}
            >
              <option value="">Todas</option>
              {RARITIES.map((r) => (
                <option key={r} value={r}>{r}</option>
              ))}
            </select>
          </label>

        </div>
        <fieldset className="filter-group">
          <legend>Costo <span>Exacto o rango · 0 a 10</span></legend>
          <div className="form-grid">
          <label>
            Costo exacto
            <input
              type="number" min="0" max="10" step="1"
              value={filters.cost}
              onChange={(e) => update("cost", e.target.value)}
            />
          </label>

          <label>
            Costo mínimo
            <input
              type="number" min="0" max="10" step="1"
              disabled={filters.cost !== ""}
              value={filters.minCost}
              onChange={(e) => update("minCost", e.target.value)}
            />
          </label>

          <label>
            Costo máximo
            <input
              type="number" min="0" max="10" step="1"
              disabled={filters.cost !== ""}
              value={filters.maxCost}
              onChange={(e) => update("maxCost", e.target.value)}
            />
          </label>

          </div>
        </fieldset>
        <fieldset className="filter-group">
          <legend>Poder <span>Exacto o rango · −20 a 20</span></legend>
          <div className="form-grid">
          <label>
            Poder exacto
            <input
              type="number" min="-20" max="20" step="1"
              value={filters.power}
              onChange={(e) => update("power", e.target.value)}
            />
          </label>

          <label>
            Poder mínimo
            <input
              type="number" min="-20" max="20" step="1"
              disabled={filters.power !== ""}
              value={filters.minPower}
              onChange={(e) => update("minPower", e.target.value)}
            />
          </label>

          <label>
            Poder máximo
            <input
              type="number" min="-20" max="20" step="1"
              disabled={filters.power !== ""}
              value={filters.maxPower}
              onChange={(e) => update("maxPower", e.target.value)}
            />
          </label>

          </div>
        </fieldset>
        <fieldset className="filter-group">
          <legend>Habilidad <span>Opcional · HAB-000 para Sin Habilidad</span></legend>
          <div className="form-grid">
          <label>
            Código de habilidad
            <input
              value={filters.abilityCode}
              onChange={(e) => update("abilityCode", e.target.value)}
              placeholder="HAB-000"
            />
          </label>

          <label>
            Disparador
            <input
              value={filters.abilityTrigger}
              onChange={(e) => update("abilityTrigger", e.target.value)}
              placeholder="Código del disparador"
            />
          </label>

          <label>
            Efecto de habilidad
            <input
              value={filters.abilityEffect}
              onChange={(e) => update("abilityEffect", e.target.value)}
              placeholder="Código del efecto"
            />
          </label>
          </div>
        </fieldset>

        {error && <p className="alert error" role="alert">{error}</p>}

        <div className="actions">
          <button disabled={loading} type="submit">
            <Icon name="search" />
            {loading ? "Consultando..." : "Buscar cartas"}
          </button>
          <button
            type="button"
            className="button-secondary"
            disabled={loading}
            onClick={clear}
          >
            Limpiar
          </button>
        </div>
      </form>

      <section className="panel results-panel" aria-busy={loading}>
        <div className="section-heading">
          <h2>Resultados</h2>
          {searched && (
            <span>{cards.length} de {total} cartas</span>
          )}
        </div>

        {cards.length === 0 && (
          <div className="empty-state" role="status">
            <span className="empty-icon"><Icon name="search" /></span>
            <h3>{loading ? "Buscando en la arena…" : searched ? "No encontramos coincidencias" : "Tu catálogo, a un clic"}</h3>
            <p>{loading ? "Estamos consultando las cartas." : searched ? "Prueba con otros criterios o limpia los filtros." : "Combina los filtros o busca sin criterios para ver todas las cartas."}</p>
          </div>
        )}

        <div className="cards-grid">
          {cards.map((card) => (
            <article className="catalog-card" key={card.code} data-type={card.type}>
              {card.imageUrl ? (
                <img
                  src={card.imageUrl.startsWith("/")
                    ? `${API_BASE_URL}${card.imageUrl}`
                    : card.imageUrl}
                  alt={`Carta ${card.name}`}
                  loading="lazy"
                />
              ) : (
                <div className="image-placeholder">
                  Imagen: {card.imageStatus}
                </div>
              )}

              <div className="catalog-card-body">
                <small>{card.code}</small>
                <h3>{card.name}</h3>
                <div className="card-tags"><span className="type-tag">{card.type}</span><span>{card.rarity}</span></div>
                <dl className="card-stats">
                  <div><dt>Costo</dt><dd>{card.cost}</dd></div>
                  <div><dt>Poder</dt><dd>{card.power}</dd></div>
                </dl>
                <p><b>Imagen:</b> {card.imageStatus}</p>
                <p><b>Habilidad:</b> {card.abilityName}</p>
                <p><b>Código:</b> {card.abilityCode}</p>
                <p><b>Disparador:</b> {card.abilityTrigger ?? "N/A"}</p>
                <p><b>Efecto:</b> {card.abilityEffect ?? "N/A"}</p>
                <details>
                  <summary>Parámetros de habilidad</summary>
                  <pre>{formatParameters(card.abilityParameters)}</pre>
                </details>
              </div>
            </article>
          ))}
        </div>

        {nextPosition !== null && (
          <div className="actions">
            <button
              disabled={loading}
              onClick={() => void loadMore()}
            >
              {loading ? "Cargando..." : "Cargar más"}
            </button>
          </div>
        )}
      </section>
    </section>
  );
}
