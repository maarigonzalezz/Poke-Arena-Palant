type IconName = "search" | "card" | "location" | "arrow";

/** Iconos decorativos; el control que los contiene aporta el nombre accesible. */
export default function Icon({ name }: { name: IconName }) {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      {name === "search" && <><circle cx="10.5" cy="10.5" r="6.5" /><path d="m16 16 5 5" /></>}
      {name === "card" && <><rect x="5" y="3" width="14" height="18" rx="3" /><path d="M9 12h6m-3-3v6" /></>}
      {name === "location" && <><path d="M20 10c0 6-8 11-8 11S4 16 4 10a8 8 0 1 1 16 0Z" /><circle cx="12" cy="10" r="2.5" /></>}
      {name === "arrow" && <path d="m9 5 7 7-7 7" />}
    </svg>
  );
}
