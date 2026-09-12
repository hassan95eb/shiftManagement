export function Brand({ compact = false }: { compact?: boolean }) {
  return (
    <span className={`brand ${compact ? 'brand--compact' : ''}`}>
      <span className="brand__mark" aria-hidden="true"><i /><i /><i /></span>
      {!compact && <strong>شیفت‌فلو</strong>}
    </span>
  );
}
