import { useState, type FormEvent } from "react";
import type { CreateWorkItemRequest } from "../types";
import { QuickDateButtons } from "./QuickDateButtons";

interface Props {
  onAdd: (item: CreateWorkItemRequest) => Promise<void>;
}

/** Priority dropdown options: word shown to the user, number sent to the API. */
const PRIORITY_OPTIONS = [
  { label: "Critical", value: 1 },
  { label: "Warning", value: 2 },
  { label: "Acceptable", value: 3 }
] as const;

const DEFAULT_PRIORITY = PRIORITY_OPTIONS[0].value;

/** Deliberately minimal: title + body + priority + completion date, so capturing a
 * stray thought takes a few seconds, not a few minutes. */
export function AddItemForm({ onAdd }: Props) {
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [reference, setReference] = useState("");
  const [priority, setPriority] = useState<number>(DEFAULT_PRIORITY);
  const [completionDate, setCompletionDate] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!title.trim() || submitting) return;

    setSubmitting(true);
    try {
      await onAdd({
        title: title.trim(),
        body: body.trim() || null,
        reference: reference.trim() || null,
        priority,
        completionDueAt: completionDate ? new Date(completionDate).toISOString() : null
      });
      setTitle("");
      setBody("");
      setReference("");
      setPriority(DEFAULT_PRIORITY);
      setCompletionDate("");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form className="add-item-form" onSubmit={handleSubmit}>
      <input
        type="text"
        placeholder="What needs doing?"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        required
      />
      <textarea
        placeholder="Details (optional)"
        value={body}
        onChange={(e) => setBody(e.target.value)}
        rows={2}
      />
      <input
        type="url"
        placeholder="Reference link (optional)"
        value={reference}
        onChange={(e) => setReference(e.target.value)}
      />
      <div className="add-item-row">
        <label>
          Priority
          <select
            value={priority}
            onChange={(e) => setPriority(Number(e.target.value))}
          >
            {PRIORITY_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
        </label>
        <label>
          Due
          <input
            type="date"
            value={completionDate}
            onChange={(e) => setCompletionDate(e.target.value)}
          />
        </label>
        <QuickDateButtons onSelect={setCompletionDate} />
      </div>
      <button type="submit" disabled={submitting || !title.trim()}>
        Add item
      </button>
    </form>
  );
}
