import { useState } from "react";
import type { UpdateWorkItemRequest, WorkItem } from "../types";
import { WorkItemList } from "./WorkItemList";

interface Props {
  items: WorkItem[];
  onUpdate: (id: string, patch: UpdateWorkItemRequest) => Promise<void>;
  onComplete: (id: string) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
}

/**
 * A collapsible view of items that can't be started yet (a future "Can't start until"
 * date). They're kept out of the main list and nudges - there's nothing to act on until
 * that date - but stay reachable here for review, or to pull one forward by clearing or
 * moving up its start date.
 */
export function ScheduledItems({ items, onUpdate, onComplete, onDelete }: Props) {
  const [expanded, setExpanded] = useState(false);

  if (items.length === 0) {
    return null;
  }

  return (
    <div className="scheduled-items">
      <button type="button" className="link-button" onClick={() => setExpanded((v) => !v)}>
        {expanded ? "Hide scheduled items" : `Show scheduled items (${items.length})`}
      </button>

      {expanded && (
        <div className="scheduled-items-body">
          <p className="scheduled-items-hint">
            Not shown in the main list or nudges until their start date - there's nothing to do yet.
          </p>
          <WorkItemList items={items} onUpdate={onUpdate} onComplete={onComplete} onDelete={onDelete} />
        </div>
      )}
    </div>
  );
}
