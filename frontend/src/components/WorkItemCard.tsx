import { useState } from "react";
import type { UpdateWorkItemRequest, WorkItem, WorkItemCategory } from "../types";

interface Props {
  item: WorkItem;
  onUpdate: (id: string, patch: UpdateWorkItemRequest) => Promise<void>;
  onComplete: (id: string) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
}

const CATEGORIES: WorkItemCategory[] = ["Critical", "Warning", "Acceptable"];

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

  return (
    <li className={`work-item category-${item.category.toLowerCase()}`}>
      <div className="work-item-header" onClick={() => setExpanded((v) => !v)}>
        <span className="work-item-dot" aria-hidden="true" />
        <span className="work-item-title">{item.title}</span>
        <span className="work-item-priority" title="Priority">
          P{item.priority}
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
          </dl>

          <div className="work-item-controls">
            <label>
              Reference link
              <input
                type="url"
                placeholder="https://…"
                defaultValue={item.reference ?? ""}
                onBlur={(e) =>
                  void onUpdate(item.id, {
                    referenceSet: true,
                    reference: e.target.value.trim() || null
                  })
                }
              />
            </label>

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
                Done
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
