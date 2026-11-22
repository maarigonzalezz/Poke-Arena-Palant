import { useEffect, useState } from "react";
import { getPlayerCatalog, getPlayerCollection, type PlayerCardPage, type PlayerCatalogFilters } from "../services/playerApi";
import type { PlayerCatalogCard } from "../pages/playerCatalogTypes";

const EMPTY_PAGE: PlayerCardPage = { items: [], nextPosition: null, totalCount: 0 };

/** Mantiene cursores anteriores y cancela respuestas obsoletas al cambiar filtros o sección. */
export function usePlayerCardPage(enabled: boolean, collection: boolean, filters: PlayerCatalogFilters) {
  const { type, cost, ownedOnly, pageSize = 12 } = filters;
  const key = JSON.stringify([collection, type, cost, ownedOnly, pageSize]);
  const [navigation, setNavigation] = useState({ key, cursors: [0], index: 0 });
  const [attempt, setAttempt] = useState(0);
  const [result, setResult] = useState<{ key: string; data: PlayerCardPage; error: string }>();
  const [knownCards, setKnownCards] = useState<PlayerCatalogCard[]>([]);
  const current = navigation.key === key ? navigation : { key, cursors: [0], index: 0 };
  const after = current.cursors[current.index];
  const requestKey = JSON.stringify([key, after, attempt]);

  useEffect(() => {
    if (!enabled) return;
    const controller = new AbortController();
    const request = collection
      ? getPlayerCollection(pageSize, after, controller.signal)
      : getPlayerCatalog({ type, cost, ownedOnly, pageSize, after }, controller.signal);
    request.then(data => {
      if (controller.signal.aborted) return;
      setResult({ key: requestKey, data, error: "" });
      // Conserva las cartas ya vistas para las selecciones del constructor de mazos.
      setKnownCards(previous => [...new Map([...previous, ...data.items].map(card => [card.code, card])).values()]);
    }).catch((cause: unknown) => {
      if (!controller.signal.aborted)
        setResult({ key: requestKey, data: EMPTY_PAGE, error: cause instanceof Error ? cause.message : "No se pudieron cargar las cartas." });
    });
    return () => controller.abort();
  }, [enabled, collection, type, cost, ownedOnly, pageSize, after, requestKey]);

  const ready = result?.key === requestKey;
  const data = ready ? result.data : EMPTY_PAGE;
  const loading = enabled && !ready;
  return {
    ...data, knownCards, loading, error: ready ? result.error : "", page: current.index,
    retry: () => setAttempt(value => value + 1),
    reset: () => setNavigation({ key, cursors: [0], index: 0 }),
    previous: () => setNavigation({ ...current, index: Math.max(0, current.index - 1) }),
    next: () => {
      if (!loading && data.nextPosition !== null)
        setNavigation({ key, cursors: [...current.cursors.slice(0, current.index + 1), data.nextPosition], index: current.index + 1 });
    },
  };
}
