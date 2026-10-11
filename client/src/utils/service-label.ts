/** Format a service label for display without changing its identifier. */
export function displayServiceName(value?: string | null): string {
  const label = (value ?? '').replace(/_+/g, ' ').replace(/\s+/g, ' ').trim();
  return label ? label[0].toLocaleUpperCase('fr-FR') + label.slice(1) : '';
}
