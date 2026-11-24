import type { PlayerCatalogCard } from "../pages/playerCatalogTypes";
import CardImage from "./CardImage";

type Props = {
  card: PlayerCatalogCard;
  quantity?: number;
  variant?: "full" | "mini";
  selected?: boolean;
  canAdd?: boolean;
  deckBusy?: boolean;
  onDeckToggle?: () => void;
};

// Una misma carta se presenta en asignación inicial, catálogo, colección y mazos.
export default function PlayerCard({ card, quantity = card.quantity, variant = "full", selected, canAdd, deckBusy, onDeckToggle }: Props) {
  if (variant === "mini") return (
    <div className="mini-card" title={card.name}>
      <CardImage card={card} small />
    </div>
  );
  return (
    <article className={`player-card type-${card.type.toLowerCase()}`} data-type={card.type}>
      <div className="player-card-art">
        <span className="player-card-code">{card.code}</span>
        <CardImage card={card} className="player-card-image" />
        <span className="player-card-art-name">{card.name}</span>
        <span className="player-card-type">{card.type}</span>
      </div>
      <div className="player-card-content">
        <div className="player-card-title"><h3>{card.name}</h3><span className={`rarity-tag rarity-${card.rarity.toLowerCase().replace(" ", "-")}`}>{card.rarity}</span></div>
        <p className="player-card-ability">{card.abilityName}</p>
        <div className="player-card-stats"><span><small>COSTO</small><strong>⚡ {card.cost}</strong></span><span><small>PODER</small><strong>✦ {card.power}</strong></span></div>
        <div className={`owned-status ${quantity ? "is-owned" : ""}`}>
          <span aria-hidden="true">{quantity ? "✓" : "○"}</span>
          {quantity ? `En tu colección · ${quantity} ${quantity === 1 ? "copia" : "copias"}` : "Aún no la tienes"}
        </div>
        {onDeckToggle && <button type="button" className={`deck-toggle ${selected ? "is-selected" : ""}`}
          disabled={deckBusy || !quantity || (!selected && !canAdd)} onClick={onDeckToggle}>
          {selected ? "Quitar del mazo" : canAdd ? "＋ Agregar al mazo" : "Mazo completo"}
        </button>}
      </div>
    </article>
  );
}
