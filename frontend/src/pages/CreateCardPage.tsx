
import { useEffect, useRef, useState } from "react";
import {
  CARD_TYPES,
  RARITIES,
  type Ability,
  type CardType,
  type CardRarity,
  type CreateCardInput,
} from "../types/catalogs";
import {
  createCard,
  getAbilities,
  IS_MOCK_MODE,
} from "../services/catalogApi";
import {
  validateImage,
} from "../services/imageValidation";

const NO_ABILITY: Ability = {
  code: "HAB-000",
  name: "Sin Habilidad",
  trigger: null,
  effect: null,
  parameters: {},
  restrictedCardType: null,
};

/** Valida datos, habilidad e imagen antes de crear una carta mediante el servicio activo. */
export default function CreateCardPage() {
  const [name, setName] = useState("");
  const [cost, setCost] = useState("0");
  const [power, setPower] = useState("0");
  const [type, setType] = useState<CardType>("Fuego");
  const [rarity, setRarity] = useState<CardRarity>("Común");
  const [abilityCode, setAbilityCode] = useState("HAB-000");
  const [abilities, setAbilities] = useState<Ability[]>([NO_ABILITY]);
  const [abilitiesError, setAbilitiesError] = useState("");
  const [loadingAbilities, setLoadingAbilities] = useState(true);
  const [image, setImage] = useState<File | null>(null);
  const [preview, setPreview] = useState("");
  const [imageInfo, setImageInfo] = useState("");
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [saving, setSaving] = useState(false);

  // Conserva la clave en reintentos; se renueva al editar o confirmar la creación.
  const keyRef = useRef(crypto.randomUUID());

  useEffect(() => {
    // Ignora respuestas que llegan después de abandonar la pantalla.
    let active = true;

    getAbilities()
      .then((list) => {
        if (!active) return;
        setAbilities(list);
      })
      .catch((e: unknown) => {
        if (!active) return;
        setAbilitiesError(
          e instanceof Error
            ? e.message
            : "No se pudo cargar el catálogo."
        );
      })
      .finally(() => { if (active) setLoadingAbilities(false); });

    return () => { active = false; };
  }, []);

  const selectedAbility = abilities.find(
    (a) => a.code === abilityCode
  );

  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview); }, [preview]);

  function changed() {
    setError("");
    setSuccess("");
    // Cambió la operación: no reutilizamos la clave anterior.
    keyRef.current = crypto.randomUUID();
  }

  async function chooseImage(file?: File) {
    if (preview) URL.revokeObjectURL(preview);
    setImage(null);
    setPreview("");
    setImageInfo("");
    changed();

    if (!file) return;

    try {
      const info = await validateImage(file, "card");
      setImage(file);
      setPreview(info.previewUrl);
      setImageInfo(`${info.width} × ${info.height} px`);
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "Imagen inválida."
      );
    }
  }

  async function submit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (saving || loadingAbilities) return;
    setError("");
    setSuccess("");

    if (!name.trim()) {
      setError("El nombre es obligatorio.");
      return;
    }

    const cardCost = Number(cost);
    const cardPower = Number(power);

    if (cost.trim() === "" || !Number.isInteger(cardCost) ||
        cardCost < 0 || cardCost > 10) {
      setError("El costo debe estar entre 0 y 10.");
      return;
    }

    if (power.trim() === "" || !Number.isInteger(cardPower) ||
        cardPower < -20 || cardPower > 20) {
      setError("El poder debe estar entre -20 y 20.");
      return;
    }

    if (!selectedAbility) {
      setError("Seleccione una habilidad existente.");
      return;
    }

    if (selectedAbility.restrictedCardType && selectedAbility.restrictedCardType !== type) {
      setError("La habilidad no corresponde al tipo de carta.");
      return;
    }

    if (!image) {
      setError("Seleccione una imagen válida.");
      return;
    }

    const payload: CreateCardInput = {
      name: name.trim(),
      cost: cardCost,
      power: cardPower,
      type,
      rarity,
      abilityCode,
      image,
    };

    setSaving(true);

    try {
      const result = await createCard(
        payload,
        keyRef.current
      );

      setSuccess(
        IS_MOCK_MODE
          ? "Carta guardada en la simulación. Ya puede encontrarla en Buscar Carta."
          : `La API confirmó la creación de la carta. ${JSON.stringify(result)}`
      );

      keyRef.current = crypto.randomUUID();
    } catch (e) {
      setError(
        e instanceof Error
          ? e.message
          : "Error al crear la carta."
      );
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="page">
      <header className="page-heading">
        <p className="eyebrow">Administración / Catálogo</p>
        <h1>Crear carta</h1>
        <p>Registre una nueva carta para Poké-Arena.</p>
      </header>

      <form className="panel" onSubmit={(e) => void submit(e)}>
        <h2>Información de la carta</h2>

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
            Tipo *
            <select
              value={type}
              onChange={(e) => {
                changed();
                setType(e.target.value as CardType);
                setAbilityCode("HAB-000");
              }}
            >
              {CARD_TYPES.map((t) => (
                <option key={t}>{t}</option>
              ))}
            </select>
          </label>

          <label>
            Rareza *
            <select
              value={rarity}
              onChange={(e) => {
                changed();
                setRarity(e.target.value as CardRarity);
              }}
            >
              {RARITIES.map((r) => (
                <option key={r}>{r}</option>
              ))}
            </select>
          </label>

          <label>
            Costo (0 a 10) *
            <input
              required type="number" min="0" max="10" step="1"
              value={cost}
              onChange={(e) => {
                changed();
                setCost(e.target.value);
              }}
            />
          </label>

          <label>
            Poder (-20 a 20) *
            <input
              required type="number" min="-20" max="20" step="1"
              value={power}
              onChange={(e) => {
                changed();
                setPower(e.target.value);
              }}
            />
          </label>

          <label>
            Habilidad *
            <select
              required
              value={abilityCode}
              onChange={(e) => {
                changed();
                setAbilityCode(e.target.value);
              }}
            >
              <option value="">Seleccione</option>
              {abilities.map((a) => (
                <option key={a.code} value={a.code} disabled={!!a.restrictedCardType && a.restrictedCardType !== type}>
                  {a.code} — {a.name}
                </option>
              ))}
            </select>
          </label>
        </div>

        {abilitiesError && (
          <p className="alert error" role="alert">
            No se pudo verificar el catálogo de habilidades: {abilitiesError}
          </p>
        )}

        {selectedAbility && (
          <div className="info-box">
            <h3>Habilidad seleccionada</h3>
            <p><b>Código:</b> {selectedAbility.code}</p>
            <p><b>Nombre:</b> {selectedAbility.name}</p>
            <p>
              <b>Disparador:</b>{" "}
              {selectedAbility.trigger ?? "Ninguno"}
            </p>
            <p>
              <b>Efecto:</b>{" "}
              {selectedAbility.effect ?? "Ninguno"}
            </p>
            <p>
              <b>Tipo afectado:</b>{" "}
              {selectedAbility.restrictedCardType ?? "Todos"}
            </p>
            <pre>
              {JSON.stringify(
                selectedAbility.parameters,
                null,
                2
              )}
            </pre>
          </div>
        )}

        <h2>Imagen de la carta</h2>

        <p className="help-text">
          PNG, JPG o WebP. Máximo 5 MB.
          Proporción 5:7 y resolución mínima 600 × 840.
        </p>

        <input
          aria-label="Imagen de la carta"
          type="file"
          accept="image/png,image/jpeg,image/webp"
          onChange={(e) => {
            void chooseImage(e.target.files?.[0]);
          }}
        />

        {preview && (
          <div className="image-preview">
            <img src={preview} alt="Vista previa de la carta" />
            <p>{imageInfo}</p>
            <p>
              {IS_MOCK_MODE ? "La simulación guarda una miniatura local de 200 × 280 píxeles." : "El servidor generará la miniatura de 200 × 280 píxeles."}
            </p>
          </div>
        )}

        {error && <p className="alert error" role="alert">{error}</p>}
        {success && <p className="alert success" role="status">{success}</p>}

        <div className="actions">
          <button disabled={saving || loadingAbilities || !!abilitiesError} type="submit">
            {saving ? "Guardando..." : loadingAbilities ? "Cargando habilidades..." : "Crear carta"}
          </button>
        </div>
      </form>
    </section>
  );
}
