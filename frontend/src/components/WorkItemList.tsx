import type { UpdateWorkItemRequest, WorkItem } from "../types";
import { WorkItemCard } from "./WorkItemCard";

interface Props {
  items: WorkItem[];
  onUpdate: (id: string, patch: UpdateWorkItemRequest) => Promise<void>;
  onComplete: (id: string) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
}

/** Items already arrive pre-sorted/pre-grouped from the API (Critical, then Warning,
 * then Acceptable); this just renders the flat list in that order. */
export function WorkItemList({ items, onUpdate, onComplete, onDelete }: Props) {
  if (items.length === 0) {
    return <p className="empty-state">Nothing outstanding. Add something above.</p>;
  }

  return (
    <ul className="work-item-list">
      {items.map((item) => (
        <WorkItemCard
          key={item.id}
          item={item}
          onUpdate={onUpdate}
          onComplete={onComplete}
          onDelete={onDelete}
        />
      ))}
    </ul>
  );
}
