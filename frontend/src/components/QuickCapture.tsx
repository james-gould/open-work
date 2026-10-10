import { useRef, useState, type FormEvent } from "react";

interface Props {
  onCapture: (title: string) => Promise<void>;
}

/** A single input, always visible, for parking a stray thought with zero friction - the
 * full AddItemForm below is for when there's a moment to add details. */
export function QuickCapture({ onCapture }: Props) {
  const [title, setTitle] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    const trimmed = title.trim();
    if (!trimmed || submitting) return;

    setSubmitting(true);
    try {
      await onCapture(trimmed);
      setTitle("");
      inputRef.current?.focus();
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="quick-capture" onSubmit={handleSubmit}>
      <input
        ref={inputRef}
        type="text"
        placeholder="Park a distraction for later…"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
      />
      <button type="submit" disabled={submitting || !title.trim()}>
        Park it
      </button>
    </form>
  );
}
