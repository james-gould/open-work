import type { CSSProperties } from "react";

interface Props {
  onSelect: (isoDate: string) => void;
  style?: CSSProperties;
}

const OPTIONS = [
  { label: "+1 day", days: 1 },
  { label: "+3 days", days: 3 },
  { label: "+7 days", days: 7 }
];

/** Quick-select buttons that fill a date input with "today + N days" so adding an item stays fast. */
export function QuickDateButtons({ onSelect, style }: Props) {
  return (
    <div className="quick-date-buttons" style={style}>
      {OPTIONS.map((opt) => (
        <button
          key={opt.days}
          type="button"
          className="quick-date-btn"
          onClick={() => {
            const d = new Date();
            d.setDate(d.getDate() + opt.days);
            onSelect(d.toISOString().slice(0, 10));
          }}
        >
          {opt.label}
        </button>
      ))}
    </div>
  );
}
