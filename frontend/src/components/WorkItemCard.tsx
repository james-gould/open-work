import { useState } from "react";
import {
  PRIORITY_OPTIONS,
  dateTimeInputToIso,
  isoToDateTimeInput,
  priorityLabel,
  priorityOptionValue
} from "../priority";
import type { UpdateWorkItemRequest, WorkItem, WorkItemCategory, WorkItemStatus } from "../types";
import { QuickDateButtons } from "./QuickDateButtons";

interface Props {
  item: WorkItem;
  onUpdate: (id: string, patch: UpdateWorkItemRequest) => Promise<void>;
  onComplete: (id: string) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
}

const CATEGORIES: WorkItemCategory[] = ["Critical", "Warning", "Acceptable"];

// Done is reached via the "Mark as done" button so it goes through the complete endpoint,
// rather than being selectable here alongside the auto-saving progression states below.
const STATUSES: { value: WorkItemStatus; label: string }[] = [
  { value: "Backlog", label: "Not started" },
  { value: "InProgress", label: "In progress" },
  { value: "Blocked", label: "Blocked" },
  { value: "Waiting", label: "Waiting on someone/something" }
];

function formatDate(iso: string | null): string {
  if (!iso) return "—";
  return new Date(iso).toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit"
  });
}

export function WorkItemCard({ item, onUpdate, onComplete, onDelete }: Props) {
  const [expanded, setExpanded] = useState(false);
  const statusLabel = STATUSES.find((s) => s.value === item.status)?.label;
  // Remount uncontrolled text inputs whenever the server copy changes.
  const editKey = item.updatedAt;

  return (
    <li className={`work-item category-${item.category.toLowerCase()}`}>
      <div className="work-item-header" onClick={() => setExpanded((v) => !v)}>
        <span className="work-item-dot" aria-hidden="true" />
        <span className="work-item-title">{item.title}</span>
        <span className="work-item-header-meta">
          {item.status !== "Backlog" && (
            <span className={`work-item-status status-${item.status.toLowerCase()}`}>{statusLabel}</span>
          )}
          <span className="work-item-priority" title="Priority">
            {priorityLabel(item.priority)}
          </span>
          {item.completionDueAt && (
            <span className="work-item-due" title="Due date">
              Due {formatDate(item.completionDueAt)}
            </span>
          )}
          {item.reference && (
            <a
              className="work-item-reference-icon"
              href={item.reference}
              target="_blank"
              rel="noopener noreferrer"
              aria-label={`Open reference for ${item.title}`}
              title="Open reference"
              onClick={(e) => e.stopPropagation()}
            >
              <svg aria-hidden="true" viewBox="0 0 24 24">
                <path d="M14 3h7v7h-2V6.41l-9.29 9.3-1.42-1.42 9.3-9.29H14V3Z" />
                <path d="M5 5h6V3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-6h-2v6H5V5Z" />
              </svg>
            </a>
          )}
        </span>
      </div>

      {expanded && (
        <div className="work-item-details">
          {item.body && <p className="work-item-body">{item.body}</p>}
          {item.reference && (
            <p className="work-item-reference">
              <a href={item.reference} target="_blank" rel="noopener noreferrer" onClick={(e) => e.stopPropagation()}>
                {item.reference}
              </a>
            </p>
          )}

          <dl className="work-item-meta">
            <dt>Requires action since</dt>
            <dd>{formatDate(item.requiresActionAt)}</dd>
            <dt>Due</dt>
            <dd>{formatDate(item.completionDueAt)}</dd>
            {item.startAt && (
              <>
                <dt>Can't start until</dt>
                <dd>{formatDate(item.startAt)}</dd>
              </>
            )}
          </dl>

          <p className="work-item-autosave-hint">Changes below save automatically as you make them.</p>

          <div className="work-item-controls">
            <label>
              Title
              <input
                key={`title-${editKey}`}
                type="text"
                defaultValue={item.title}
                onBlur={(e) => {
                  const value = e.target.value.trim();
                  if (!value) {
                    e.target.value = item.title;
                  } else if (value !== item.title) {
                    void onUpdate(item.id, { title: value });
                  }
                }}
              />
            </label>

            <label>
              Details
              <textarea
                key={`body-${editKey}`}
                rows={2}
                defaultValue={item.body ?? ""}
                onBlur={(e) => {
                  const value = e.target.value.trim() || null;
                  if (value !== item.body) void onUpdate(item.id, { bodySet: true, body: value });
                }}
              />
            </label>

            <label>
              Reference link
              <input
                key={`reference-${editKey}`}
                type="url"
                placeholder="https://…"
                defaultValue={item.reference ?? ""}
                onBlur={(e) => {
                  const value = e.target.value.trim() || null;
                  if (value !== item.reference) {
                    void onUpdate(item.id, { referenceSet: true, reference: value });
                  }
                }}
              />
            </label>

            <label>
              Status
              <select
                value={item.status}
                onChange={(e) => void onUpdate(item.id, { status: e.target.value as WorkItemStatus })}
              >
                {STATUSES.map((s) => (
                  <option key={s.value} value={s.value}>
                    {s.label}
                  </option>
                ))}
              </select>
            </label>

            <label>
              Priority
              <select
                value={priorityOptionValue(item.priority)}
                onChange={(e) => void onUpdate(item.id, { priority: Number(e.target.value) })}
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
                type="datetime-local"
                value={isoToDateTimeInput(item.completionDueAt)}
                onChange={(e) =>
                  void onUpdate(item.id, {
                    completionDueAtSet: true,
                    completionDueAt: dateTimeInputToIso(e.target.value)
                  })
                }
              />
            </label>
            <QuickDateButtons
              onSelect={(date) =>
                void onUpdate(item.id, { completionDueAtSet: true, completionDueAt: dateTimeInputToIso(date) })
              }
            />

            <label>
              Can't start until
              <input
                type="datetime-local"
                value={isoToDateTimeInput(item.startAt)}
                onChange={(e) =>
                  void onUpdate(item.id, {
                    startAtSet: true,
                    startAt: dateTimeInputToIso(e.target.value)
                  })
                }
              />
            </label>
            <QuickDateButtons
              onSelect={(date) => void onUpdate(item.id, { startAtSet: true, startAt: dateTimeInputToIso(date) })}
            />
            <p className="work-item-field-hint">
              Keeps this off the main list and nudges until that date, even if it's Critical by its due date.
            </p>

            <label>
              Category override
              <select
                value={item.isManualCategory ? item.category : ""}
                onChange={(e) => {
                  const value = e.target.value;
                  if (value === "") {
                    void onUpdate(item.id, { manualCategorySet: true, manualCategory: null });
                  } else {
                    void onUpdate(item.id, {
                      manualCategorySet: true,
                      manualCategory: value as WorkItemCategory
                    });
                  }
                }}
              >
                <option value="">Auto ({item.category})</option>
                {CATEGORIES.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>
            </label>

            <label>
              Mark action required now
              <input
                type="checkbox"
                checked={item.requiresActionAt !== null}
                onChange={(e) =>
                  void onUpdate(item.id, {
                    requiresActionAtSet: true,
                    requiresActionAt: e.target.checked ? new Date().toISOString() : null
                  })
                }
              />
            </label>

            <div className="work-item-actions">
              <button type="button" onClick={() => void onComplete(item.id)}>
                Mark as done
              </button>
              <button type="button" className="danger" onClick={() => void onDelete(item.id)}>
                Delete
              </button>
            </div>
          </div>
        </div>
      )}
    </li>
  );
}
