import { useCallback, useEffect, useState } from "react";
import { api } from "../api";
import type { WorkItem } from "../types";

interface Props {
  onReopen: (id: string) => Promise<void>;
}

function formatDate(iso: string | null): string {
  if (!iso) return "—";
  return new Date(iso).toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit"
  });
}

/** A collapsible "Done" view: browse completed items, reopen one by mistake, or download
 * a CSV of everything completed in a chosen date range. */
export function DoneReport({ onReopen }: Props) {
  const [expanded, setExpanded] = useState(false);
  const [items, setItems] = useState<WorkItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [downloading, setDownloading] = useState(false);

  const loadDone = useCallback(async () => {
    setLoading(true);
    try {
      const all = await api.listWorkItems(true);
      setItems(all.filter((i) => i.status === "Done").sort((a, b) =>
        (b.completedAt ?? "").localeCompare(a.completedAt ?? "")
      ));
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to load done items.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (expanded) void loadDone();
  }, [expanded, loadDone]);

  async function handleReopen(id: string) {
    await onReopen(id);
    await loadDone();
  }

  async function handleDownload() {
    setDownloading(true);
    setError(null);
    try {
      const fromIso = from ? new Date(from).toISOString() : undefined;
      const toIso = to ? new Date(to).toISOString() : undefined;
      const { blob, fileName } = await api.downloadDoneReport(fromIso, toIso);
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Couldn't download the report.");
    } finally {
      setDownloading(false);
    }
  }

  return (
    <div className="done-report">
      <button type="button" className="link-button" onClick={() => setExpanded((v) => !v)}>
        {expanded ? "Hide done items" : "Show done items"}
      </button>

      {expanded && (
        <div className="done-report-body">
          <div className="done-report-controls">
            <label>
              From
              <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
            </label>
            <label>
              To
              <input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
            </label>
            <button type="button" onClick={() => void handleDownload()} disabled={downloading}>
              {downloading ? "Downloading…" : "Download CSV"}
            </button>
          </div>

          {error && <p className="error-banner">{error}</p>}
          {loading ? (
            <p className="empty-state">Loading…</p>
          ) : items.length === 0 ? (
            <p className="empty-state">Nothing done yet.</p>
          ) : (
            <ul className="done-item-list">
              {items.map((item) => (
                <li key={item.id} className="done-item">
                  <span className="done-item-title">{item.title}</span>
                  <span className="done-item-date">{formatDate(item.completedAt)}</span>
                  <button type="button" onClick={() => void handleReopen(item.id)}>
                    Reopen
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
