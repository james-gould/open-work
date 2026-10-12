import { getToken, notifyUnauthorized } from "./auth";
import type {
  AuthStatusResponse,
  AuthTokenResponse,
  CreateWorkItemRequest,
  QuickCaptureRequest,
  UpdateWorkItemRequest,
  WorkItem
} from "./types";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken();
  const res = await fetch(path, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init?.headers ?? {})
    }
  });

  if (res.status === 401) {
    notifyUnauthorized();
  }

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

  listScheduledWorkItems: () => request<WorkItem[]>("/api/workitems/scheduled"),

  createWorkItem: (body: CreateWorkItemRequest) =>
    request<WorkItem>("/api/workitems", { method: "POST", body: JSON.stringify(body) }),

  quickCapture: (body: QuickCaptureRequest) =>
    request<WorkItem>("/api/workitems/quick", { method: "POST", body: JSON.stringify(body) }),

  updateWorkItem: (id: string, body: UpdateWorkItemRequest) =>
    request<WorkItem>(`/api/workitems/${id}`, { method: "PATCH", body: JSON.stringify(body) }),

  deleteWorkItem: (id: string) =>
    request<void>(`/api/workitems/${id}`, { method: "DELETE" }),

  completeWorkItem: (id: string) =>
    request<WorkItem>(`/api/workitems/${id}/complete`, { method: "POST" }),

  reopenWorkItem: (id: string) =>
    request<WorkItem>(`/api/workitems/${id}/reopen`, { method: "POST" }),

  markActionable: (id: string) =>
    request<WorkItem>(`/api/workitems/${id}/actionable`, { method: "POST" }),

  /** Fetches the Done-items CSV as a blob (rather than a plain `<a href>`, which can't
   * attach the Authorization header) and returns it along with the server-suggested
   * filename so the caller can trigger a browser download. */
  downloadDoneReport: async (from?: string, to?: string): Promise<{ blob: Blob; fileName: string }> => {
    const params = new URLSearchParams();
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    const query = params.toString();
    const path = `/api/workitems/report/done${query ? `?${query}` : ""}`;

    const token = getToken();
    const res = await fetch(path, {
      headers: token ? { Authorization: `Bearer ${token}` } : {}
    });

    if (res.status === 401) {
      notifyUnauthorized();
    }

    if (!res.ok) {
      const text = await res.text().catch(() => "");
      throw new Error(`Request to ${path} failed (${res.status}): ${text}`);
    }

    const disposition = res.headers.get("Content-Disposition") ?? "";
    const match = /filename="?([^";]+)"?/i.exec(disposition);
    const fileName = match?.[1] ?? "open-work-done.csv";
    return { blob: await res.blob(), fileName };
  },

  getPushPublicKey: () => request<string>("/api/push/public-key"),

  subscribePush: (subscription: PushSubscriptionJSON) =>
    request<void>("/api/push/subscribe", { method: "POST", body: JSON.stringify(subscription) }),

  unsubscribePush: (endpoint: string) =>
    request<void>("/api/push/unsubscribe", { method: "POST", body: JSON.stringify({ endpoint }) }),

  authStatus: () => request<AuthStatusResponse>("/api/auth/status"),

  setupPin: (pin: string) =>
    request<AuthTokenResponse>("/api/auth/setup", { method: "POST", body: JSON.stringify({ pin }) }),

  loginPin: (pin: string) =>
    request<AuthTokenResponse>("/api/auth/login", { method: "POST", body: JSON.stringify({ pin }) }),

  recoveryRegisterOptions: () =>
    request<PublicKeyCredentialCreationOptionsJSON>("/api/auth/recovery/register-options", { method: "POST" }),

  recoveryRegister: (attestationResponse: unknown) =>
    request<void>("/api/auth/recovery/register", {
      method: "POST",
      body: JSON.stringify(attestationResponse)
    }),

  recoveryAssertionOptions: () =>
    request<PublicKeyCredentialRequestOptionsJSON>("/api/auth/recovery/assertion-options", { method: "POST" }),

  recoveryWipe: (assertionResponse: unknown) =>
    request<void>("/api/auth/recovery/wipe", {
      method: "POST",
      body: JSON.stringify(assertionResponse)
    })
};

