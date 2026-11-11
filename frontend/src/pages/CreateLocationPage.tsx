
import { useRef, useState } from "react";
import {
  CARD_TYPES,
  type CardType,
  type EffectType,
  type EnergyMode,
  type CreateLocationInput,
} from "../types/catalogs";
import { createLocation, IS_MOCK_MODE } from "../services/catalogApi";
import ImageUpload from "../components/ImageUpload";

/** Captura una ubicación con límites de cartas, energía, efectos e imagen validada. */
export default function CreateLocationPage() {
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [maxCards, setMaxCards] = useState("4");
  const [energyMode, setEnergyMode] = useState<EnergyMode>("Fixed");
  const [extraEnergy, setExtraEnergy] = useState("0");
  const [effectType, setEffectType] = useState<EffectType>("None");

  // Estos estados contienen únicamente los parámetros de los efectos configurables.
  const [affectedType, setAffectedType] =
    useState<CardType>("Fuego");
  const [amount, setAmount] = useState("0");
  const [costType, setCostType] = useState("");
  const [restrictedTurns, setRestrictedTurns] =
    useState<number[]>([]);

  const [image, setImage] = useState<File | null>(null);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [saving, setSaving] = useState(false);

  // Reutiliza la clave si falla el envío; una edición inicia una operación distinta.
  const keyRef = useRef(crypto.randomUUID());


  function changed() {
    keyRef.current = crypto.randomUUID();
    setError("");
    setSuccess("");
  }

  // Activa o desactiva un turno y mantiene la selección ordenada.
  function toggleTurn(turn: number) {
    changed();
    setRestrictedTurns((previous) =>
      previous.includes(turn)
        ? previous.filter((t) => t !== turn)
        : [...previous, turn].sort()
    );
  }


  // Incluye únicamente los parámetros correspondientes al efecto seleccionado.
  function effectParameters(): Record<string, unknown> {
    switch (effectType) {
      case "TypeBonus":
        return {
          type: affectedType,
          power: Number(amount),
        };

      case "CostModifier":
        return {
          amount: Number(amount),
          affectedType: costType || null,
        };

      case "TurnRestriction":
        return {
          turns: restrictedTurns,
        };

      default:
        return {};
    }
  }

  // La modalidad de energía y el efecto determinan qué campos y rangos son válidos.
  function validate(): string | null {
    if (!name.trim())
      return "El nombre es obligatorio.";

    if (!description.trim())
      return "La descripción es obligatoria.";

    const limit = Number(maxCards);
    if (maxCards.trim() === "" || !Number.isInteger(limit) || limit < 0 || limit > 5)
      return "El límite de cartas debe ser de 0 a 5.";

    const energy = Number(extraEnergy);
    // La energía máxima cambia según la modalidad seleccionada.
    const energyMax = energyMode === "Fixed" ? 3 : 2;

    if (
      extraEnergy.trim() === "" || !Number.isInteger(energy) ||
      energy < 0 ||
      energy > energyMax
    ) {
      return `La energía debe estar entre 0 y ${energyMax}.`;
    }

    if (
      effectType === "TypeBonus" ||
      effectType === "CostModifier"
    ) {
      const value = Number(amount);

      if (
        amount.trim() === "" ||
        !Number.isInteger(value) ||
        value < -5 ||
        value > 5
      ) {
        return "El modificador debe estar entre -5 y +5.";
      }
    }

    if (
      effectType === "TurnRestriction" &&
      restrictedTurns.length === 0
    ) {
      return "Seleccione al menos un turno restringido.";
    }

    if (!image)
      return "Debe seleccionar una imagen válida.";

    return null;
  }

  // Envía la imagen y la configuración mediante catalogApi; el servidor asigna la identidad.
  async function submit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (saving) return;

    const problem = validate();

    if (problem) {
      setError(problem);
      return;
    }

    if (!image) return;

    // Reúne la configuración y la imagen en el contrato de creación.
    const input: CreateLocationInput = {
      name: name.trim(),
      description: description.trim(),
      maxCards: Number(maxCards),
      energyMode,
      extraEnergy: Number(extraEnergy),
      effectType,
      effectParameters: effectParameters(),
      image,
    };

    // Bloquea nuevos envíos mientras se espera la respuesta.
    setSaving(true);
    setError("");
    setSuccess("");

    try {
      const result = await createLocation(
        input,
        keyRef.current
      );

      setSuccess(
        IS_MOCK_MODE
          ? "Ubicación guardada en la simulación de este navegador."
          : `Ubicación creada con sus imágenes. ${JSON.stringify(result)}`
      );

      keyRef.current = crypto.randomUUID();
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "Error al crear la ubicación."
      );
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="page">
      <header className="page-heading">
        <p className="eyebrow">Administración / Ubicaciones</p>
        <h1>Crear ubicación</h1>
        <p>
          Configure una ubicación para partidas futuras.
        </p>
      </header>

      <form className="panel" onSubmit={(e) => void submit(e)}>
        <h2>Información general</h2>

        <div className="form-grid">
          <label>
            Nombre *
            <input
              required
              value={name}
              onChange={(e) => {
                changed();
                setName(e.target.value);
              }}
            />
          </label>

          <label>
            Límite de cartas por jugador *
            <input
              required type="number" min="0" max="5" step="1"
              value={maxCards}
              onChange={(e) => {
                changed();
                setMaxCards(e.target.value);
              }}
            />
            <span className="help-text">
              0 significa que no se pueden jugar cartas.
            </span>
          </label>

          <label className="full-width">
            Descripción *
            <textarea
              required
              rows={3}
              value={description}
              onChange={(e) => {
                changed();
                setDescription(e.target.value);
              }}
            />
          </label>
        </div>

        {/* La modalidad define cómo se interpretará la cantidad de energía. */}
        <h2>Energía adicional</h2>

        <div className="form-grid">
          <label>
            Modalidad *
            <select
              value={energyMode}
              onChange={(e) => {
                changed();
                setEnergyMode(e.target.value as EnergyMode);
                setExtraEnergy("0");
              }}
            >
              <option value="Fixed">Fija por turno</option>
              <option value="PerCard">Por carta propia</option>
            </select>
          </label>

          <label>
            Cantidad de energía *
            <input
              required type="number" min="0"
              max={energyMode === "Fixed" ? "3" : "2"}
              step="1"
              value={extraEnergy}
              onChange={(e) => {
                changed();
                setExtraEnergy(e.target.value);
              }}
            />
          </label>
        </div>

        <p className="help-text">
          {energyMode === "Fixed"
            ? "Cada jugador recibe esa energía extra al inicio del turno."
            : "Cada jugador recibe esa energía por cada carta propia en esta ubicación al inicio del turno."}
        </p>

        {/* El selector muestra los campos que necesita cada tipo de efecto. */}
        <h2>Efecto de la ubicación</h2>

        <label>
          Tipo de efecto
          <select
            value={effectType}
            onChange={(e) => {
              changed();
              setEffectType(e.target.value as EffectType);
              setAmount("0");
            }}
          >
            <option value="None">Sin efecto</option>
            <option value="TypeBonus">Bonificación por tipo</option>
            <option value="CostModifier">Modificador de costo</option>
            <option value="TurnRestriction">Restricción por turno</option>
          </select>
        </label>

        {effectType === "TypeBonus" && (
          <div className="form-grid nested">
            <label>
              Tipo beneficiado
              <select
                value={affectedType}
                onChange={(e) => {
                  changed();
                  setAffectedType(e.target.value as CardType);
                }}
              >
                {CARD_TYPES.map((t) => (
                  <option key={t}>{t}</option>
                ))}
              </select>
            </label>
            <label>
              Bonificación de poder (-5 a +5)
              <input
                type="number" min="-5" max="5" step="1"
                value={amount}
                onChange={(e) => {
                  changed();
                  setAmount(e.target.value);
                }}
              />
            </label>
          </div>
        )}

        {effectType === "CostModifier" && (
          <div className="form-grid nested">
            <label>
              Modificador de costo (-5 a +5)
              <input
                type="number" min="-5" max="5" step="1"
                value={amount}
                onChange={(e) => {
                  changed();
                  setAmount(e.target.value);
                }}
              />
            </label>
            <label>
              Tipo afectado
              <select
                value={costType}
                onChange={(e) => {
                  changed();
                  setCostType(e.target.value);
                }}
              >
                <option value="">Todos los tipos</option>
                {CARD_TYPES.map((t) => (
                  <option key={t}>{t}</option>
                ))}
              </select>
            </label>
          </div>
        )}

        {/* La selección de turnos se transforma en el arreglo turns del JSON. */}
        {effectType === "TurnRestriction" && (
          <fieldset className="nested">
            <legend>Turnos donde no se pueden jugar cartas</legend>
            <div className="turn-list">
              {[1, 2, 3, 4, 5, 6].map((turn) => (
                <label key={turn} className="turn-option">
                  <input
                    type="checkbox"
                    checked={restrictedTurns.includes(turn)}
                    onChange={() => toggleTurn(turn)}
                  />
                  Turno {turn}
                </label>
              ))}
            </div>
          </fieldset>
        )}

        <h2>Imagen de la ubicación</h2>
        {/* La ubicación reutiliza el redimensionado con sus propias dimensiones. */}
        <ImageUpload kind="location" disabled={saving}
          onChange={file => { changed(); setImage(file); }} />

        {error && <p className="alert error" role="alert">{error}</p>}
        {success && <p className="alert success" role="status">{success}</p>}

        <div className="actions">
          <button type="submit" disabled={saving || !image}>
            {saving ? "Guardando..." : "Crear ubicación"}
          </button>
        </div>
      </form>
    </section>
  );
}
