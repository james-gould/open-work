export type WorkItemCategory = "Critical" | "Warning" | "Acceptable";

export type WorkItemStatus = "Backlog" | "InProgress" | "Blocked" | "Waiting" | "Done";

export interface WorkItem {
  id: string;
  title: string;
  body: string | null;
  reference: string | null;
  externalKey: string | null;
  priority: number;
  requiresActionAt: string | null;
  completionDueAt: string | null;
  category: WorkItemCategory;
  isManualCategory: boolean;
  status: WorkItemStatus;
  isDone: boolean;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
}

export interface CreateWorkItemRequest {
  title: string;
  body?: string | null;
  reference?: string | null;
  priority: number;
  requiresActionAt?: string | null;
  completionDueAt?: string | null;
  status?: WorkItemStatus;
  manualCategory?: WorkItemCategory | null;
  externalKey?: string | null;
}

export interface UpdateWorkItemRequest {
  title?: string;
  bodySet?: boolean;
  body?: string | null;
  referenceSet?: boolean;
  reference?: string | null;
  priority?: number;
  requiresActionAtSet?: boolean;
  requiresActionAt?: string | null;
  completionDueAtSet?: boolean;
  completionDueAt?: string | null;
  manualCategorySet?: boolean;
  manualCategory?: WorkItemCategory | null;
  status?: WorkItemStatus;
  isDone?: boolean;
}

export interface QuickCaptureRequest {
  title: string;
}

export interface AuthStatusResponse {
  configured: boolean;
  hasRecovery: boolean;
}

export interface AuthTokenResponse {
  token: string;
  expiresAt: string;
}
