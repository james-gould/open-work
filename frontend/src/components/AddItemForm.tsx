import { useState, type FormEvent } from "react";
import type { CreateWorkItemRequest } from "../types";
import { QuickDateButtons } from "./QuickDateButtons";

interface Props {
  onAdd: (item: CreateWorkItemRequest) => Promise<void>;
}

/** Deliberately minimal: title + body + priority + completion date, so capturing a
 * stray thought takes a few seconds, not a few minutes. */
export function AddItemForm({ onAdd }: Props) {
  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [priority, setPriority] = useState(0);
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
        priority,
        completionDueAt: completionDate ? new Date(completionDate).toISOString() : null
      });
      setTitle("");
      setBody("");
      setPriority(0);
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
      <div className="add-item-row">
        <label>
          Priority
          <input
            type="number"
            value={priority}
            onChange={(e) => setPriority(Number(e.target.value))}
          />
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
