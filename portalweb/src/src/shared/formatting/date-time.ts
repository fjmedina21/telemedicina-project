export function localTime(value: unknown) {
  return new Intl.DateTimeFormat("es-DO", {
    timeZone:"America/Santo_Domingo",dateStyle:"short",timeStyle:"short"
  }).format(new Date(String(value)));
}
