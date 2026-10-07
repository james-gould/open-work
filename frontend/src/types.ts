export type WorkItemCategory = "Critical" | "Warning" | "Acceptable";

export interface WorkItem {
  id: string;
  title: string;
  body: string | null;
  priority: number;
  requiresActionAt: string | null;
  completionDueAt: string | null;
  category: WorkItemCategory;
  isManualCategory: boolean;
  isDone: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateWorkItemRequest {
  title: string;
  body?: string | null;
  priority: number;
  requiresActionAt?: string | null;
  completionDueAt?: string | null;
}

export interface UpdateWorkItemRequest {
  title?: string;
  bodySet?: boolean;
  body?: string | null;
  priority?: number;
  requiresActionAtSet?: boolean;
  requiresActionAt?: string | null;
  completionDueAtSet?: boolean;
  completionDueAt?: string | null;
  manualCategorySet?: boolean;
  manualCategory?: WorkItemCategory | null;
  isDone?: boolean;
}
