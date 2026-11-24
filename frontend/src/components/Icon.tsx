type IconName = "search" | "card" | "location" | "arrow" | "swords" | "refresh" | "shield" | "check" | "clock" | "bot";

/** Iconos decorativos; el control que los contiene aporta el nombre accesible. */
export default function Icon({ name }: { name: IconName }) {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      {name === "search" && <><circle cx="10.5" cy="10.5" r="6.5" /><path d="m16 16 5 5" /></>}
      {name === "card" && <><rect x="5" y="3" width="14" height="18" rx="3" /><path d="M9 12h6m-3-3v6" /></>}
      {name === "location" && <><path d="M20 10c0 6-8 11-8 11S4 16 4 10a8 8 0 1 1 16 0Z" /><circle cx="12" cy="10" r="2.5" /></>}
      {name === "arrow" && <path d="m9 5 7 7-7 7" />}
      {name === "swords" && <><path d="m14 4 6-1-1 6L7 21l-4-4L15 5M4 3l5 1 11 13-3 3L5 8 4 3M3 13l8 8m2-18 8 8" /></>}
      {name === "refresh" && <><path d="M20 7v5h-5M4 17v-5h5" /><path d="M6 7a7 7 0 0 1 12-1l2 3M4 15l2 3a7 7 0 0 0 12-1" /></>}
      {name === "shield" && <><path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6l8-3Z" /><path d="m8.5 12 2.5 2.5 4.5-5" /></>}
      {name === "check" && <><circle cx="12" cy="12" r="9" /><path d="m8 12 3 3 5-6" /></>}
      {name === "clock" && <><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></>}
      {name === "bot" && <><rect x="4" y="7" width="16" height="13" rx="4" /><path d="M12 3v4M1 12v4m22-4v4M9 16h6" /><path d="M8 12h.01M16 12h.01" /></>}
    </svg>
  );
}
