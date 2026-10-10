import { useCallback, useEffect, useState } from "react";
import { api } from "./api";
import { AddItemForm } from "./components/AddItemForm";
import { AuthGate } from "./components/AuthGate";
import { DoneReport } from "./components/DoneReport";
import { NotificationSettings } from "./components/NotificationSettings";
import { QuickCapture } from "./components/QuickCapture";
import { RecoverySettings } from "./components/RecoverySettings";
import { WorkItemList } from "./components/WorkItemList";
import type { CreateWorkItemRequest, UpdateWorkItemRequest, WorkItem } from "./types";

const POLL_INTERVAL_MS = 30_000;

export default function App() {
  return (
    <AuthGate>
      {({ logout, hasRecovery }) => <AppContent onLogout={logout} hasRecovery={hasRecovery} />}
    </AuthGate>
  );
}

interface AppContentProps {
  onLogout: () => void;
  hasRecovery: boolean;
}

function AppContent({ onLogout, hasRecovery }: AppContentProps) {
  const [items, setItems] = useState<WorkItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [recoveryKnown, setRecoveryKnown] = useState(hasRecovery);

  const refresh = useCallback(async () => {
    try {
      const data = await api.listWorkItems();
      setItems(data);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load work items.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void refresh();
    const interval = setInterval(() => void refresh(), POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [refresh]);

  async function handleAdd(request: CreateWorkItemRequest) {
    await api.createWorkItem(request);
    await refresh();
  }

  async function handleQuickCapture(title: string) {
    await api.quickCapture({ title });
    await refresh();
  }

  async function handleUpdate(id: string, patch: UpdateWorkItemRequest) {
    await api.updateWorkItem(id, patch);
    await refresh();
  }

  async function handleComplete(id: string) {
    await api.completeWorkItem(id);
    await refresh();
  }

  async function handleReopen(id: string) {
    await api.reopenWorkItem(id);
    await refresh();
  }

  async function handleDelete(id: string) {
    await api.deleteWorkItem(id);
    await refresh();
  }

  const criticalCount = items.filter((i) => i.category === "Critical").length;

  return (
    <div className="app">
      <header className="app-header">
        <h1>Open Work</h1>
        {criticalCount > 0 && <span className="critical-badge">{criticalCount} critical</span>}
        <NotificationSettings />
        <button type="button" className="link-button" onClick={onLogout}>
          Log out
        </button>
      </header>

      <QuickCapture onCapture={handleQuickCapture} />

      <AddItemForm onAdd={handleAdd} />

      {error && <p className="error-banner">{error}</p>}
      {loading ? (
        <p className="empty-state">Loading…</p>
      ) : (
        <WorkItemList
          items={items}
          onUpdate={handleUpdate}
          onComplete={handleComplete}
          onDelete={handleDelete}
        />
      )}

      <DoneReport onReopen={handleReopen} />

      <RecoverySettings hasRecovery={recoveryKnown} onRegistered={() => setRecoveryKnown(true)} />
    </div>
  );
}
