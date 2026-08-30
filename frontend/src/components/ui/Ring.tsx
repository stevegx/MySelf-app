type RingProps = {
  size: number;
  stroke: number;
  /** 0..1 progress. 0 renders just the track. */
  value?: number;
  color?: string;
  trackColor?: string;
  ariaLabel?: string;
};

export function Ring({
  size,
  stroke,
  value = 0,
  color = "var(--color-primary)",
  trackColor = "var(--color-viz-track)",
  ariaLabel,
}: RingProps) {
  const r = (size - stroke) / 2;
  const circumference = 2 * Math.PI * r;
  const clamped = Math.max(0, Math.min(1, value));

  return (
    <svg
      width={size}
      height={size}
      viewBox={`0 0 ${size} ${size}`}
      className="-rotate-90"
      role="img"
      aria-label={ariaLabel}
    >
      <circle
        cx={size / 2}
        cy={size / 2}
        r={r}
        fill="none"
        stroke={trackColor}
        strokeWidth={stroke}
      />
      {clamped > 0 ? (
        <circle
          cx={size / 2}
          cy={size / 2}
          r={r}
          fill="none"
          stroke={color}
          strokeWidth={stroke}
          strokeLinecap="round"
          strokeDasharray={circumference}
          strokeDashoffset={circumference * (1 - clamped)}
        />
      ) : null}
    </svg>
  );
}
