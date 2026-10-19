import type { CSSProperties } from "react";
import { localDateTimePlus } from "../priority";

interface Props {
  onSelect: (isoDate: string) => void;
  style?: CSSProperties;
}

const OPTIONS = [
  { label: "+2 hours", days: 0, hours: 2 },
  { label: "+1 day", days: 1, hours: 0 },
  { label: "+3 days", days: 3, hours: 0 },
  { label: "+7 days", days: 7, hours: 0 }
];

/** Quick-select buttons that fill a local date-time input with an offset from now. */
export function QuickDateButtons({ onSelect, style }: Props) {
  return (
    <div className="quick-date-buttons" style={style}>
      {OPTIONS.map((opt) => (
        <button
          key={opt.label}
          type="button"
          className="quick-date-btn"
          onClick={() => onSelect(localDateTimePlus(opt.days, opt.hours))}
        >
          {opt.label}
        </button>
      ))}
    </div>
  );
}
