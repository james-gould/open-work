import type { CreateWorkItemRequest, UpdateWorkItemRequest, WorkItem } from "./types";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(path, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {})
    }
  });

  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new Error(`Request to ${path} failed (${res.status}): ${text}`);
  }

  if (res.status === 204) {
    return undefined as T;
  }

  return (await res.json()) as T;
}

export const api = {
  listWorkItems: (includeDone = false) =>
    request<WorkItem[]>(`/api/workitems?includeDone=${includeDone}`),

  createWorkItem: (body: CreateWorkItemRequest) =>
    request<WorkItem>("/api/workitems", { method: "POST", body: JSON.stringify(body) }),

  updateWorkItem: (id: string, body: UpdateWorkItemRequest) =>
    request<WorkItem>(`/api/workitems/${id}`, { method: "PATCH", body: JSON.stringify(body) }),

  deleteWorkItem: (id: string) =>
    request<void>(`/api/workitems/${id}`, { method: "DELETE" }),

  completeWorkItem: (id: string) =>
    request<WorkItem>(`/api/workitems/${id}/complete`, { method: "POST" }),

  reopenWorkItem: (id: string) =>
    request<WorkItem>(`/api/workitems/${id}/reopen`, { method: "POST" }),

  getPushPublicKey: () => request<string>("/api/push/public-key"),

  subscribePush: (subscription: PushSubscriptionJSON) =>
    request<void>("/api/push/subscribe", { method: "POST", body: JSON.stringify(subscription) }),

  unsubscribePush: (endpoint: string) =>
    request<void>("/api/push/unsubscribe", { method: "POST", body: JSON.stringify({ endpoint }) })
};
