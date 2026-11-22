
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
import ImageUpload from "../components/ImageUpload";

// HAB-000 representa Sin Habilidad dentro del mismo catálogo de habilidades.
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
  // Solo envía el archivo después de terminar el redimensionado automático.
  const [image, setImage] = useState<File | null>(null);
  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [saving, setSaving] = useState(false);
  const [created, setCreated] = useState(false);
  const [imageUploadKey, setImageUploadKey] = useState(0);
  // Bloquea también envíos consecutivos antes de que React actualice la pantalla.
  const submitLockedRef = useRef(false);

  // Conserva la clave tras guardar y en reintentos; solo cambia para una nueva operación.
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

  // Localiza la habilidad elegida para comprobar su compatibilidad.
  const selectedAbility = abilities.find(
    (a) => a.code === abilityCode
  );


  function changed() {
    setError("");
    setSuccess("");
    // Cambió la operación: no reutilizamos la clave anterior.
    keyRef.current = crypto.randomUUID();
  }

  // Limpia la carta guardada antes de permitir una creación diferente.
  function startNewCard() {
    if (saving || !created) return;
    setName("");
    setCost("0");
    setPower("0");
    setType("Fuego");
    setRarity("Común");
    setAbilityCode("HAB-000");
    setImage(null);
    setImageUploadKey(key => key + 1);
    changed();
    setCreated(false);
    submitLockedRef.current = false;
  }


  // La validación local orienta al usuario; la API vuelve a validar antes de persistir.
  async function submit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    // Una carta confirmada no se vuelve a enviar, ni siquiera mediante Enter.
    if (submitLockedRef.current || saving || created || loadingAbilities || abilitiesError) return;
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

    // Construye la solicitud con datos editables; el código lo asigna el servidor.
    const payload: CreateCardInput = {
      name: name.trim(),
      cost: cardCost,
      power: cardPower,
      type,
      rarity,
      abilityCode,
      image,
    };

    submitLockedRef.current = true;
    setSaving(true);

    try {
      const result = await createCard(
        payload,
        keyRef.current
      );

      // El mensaje de éxito depende de la respuesta del servicio activo.
      setSuccess(
        IS_MOCK_MODE
          ? "Carta guardada en la simulación. Ya puede encontrarla en Buscar Carta."
          : `La API confirmó la creación de la carta. ${JSON.stringify(result)}`
      );

      setCreated(true);
    } catch (e) {
      // Un fallo permite reintentar con la misma clave para evitar duplicados en el servidor.
      submitLockedRef.current = false;
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
        {/* Los campos controlados mantienen el formulario sincronizado con el estado. */}
        <h2>Información de la carta</h2>

        <div className="form-grid">
          <label>
            Nombre *
            <input
              required
              disabled={saving || created}
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
              disabled={saving || created}
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
              disabled={saving || created}
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
              disabled={saving || created}
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
              disabled={saving || created}
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
              disabled={saving || created}
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

        {/* Seleccionar la imagen la adapta automáticamente al tamaño de una carta. */}
        <ImageUpload key={imageUploadKey} kind="card" disabled={saving || created}
          onChange={file => { changed(); setImage(file); }} />

        {/* Presenta el resultado del envío sin abandonar el formulario. */}
        {error && <p className="alert error" role="alert">{error}</p>}
        {success && <p className="alert success" role="status">{success}</p>}

        <div className="actions">
          <button disabled={saving || created || loadingAbilities || !!abilitiesError || !image} type="submit">
            {saving ? "Guardando..." : created ? "Carta creada" : loadingAbilities ? "Cargando habilidades..." : "Crear carta"}
          </button>
          {created && (
            <button className="button-secondary" type="button" onClick={startNewCard}>
              Crear otra carta
            </button>
          )}
        </div>
      </form>
    </section>
  );
}
