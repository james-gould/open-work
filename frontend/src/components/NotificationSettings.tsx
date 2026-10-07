import { useEffect, useState } from "react";
import { disablePushNotifications, enablePushNotifications, isPushSupported } from "../push";

export function NotificationSettings() {
  const [status, setStatus] = useState<"unknown" | "off" | "on" | "unsupported">("unknown");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!isPushSupported()) {
      setStatus("unsupported");
      return;
    }
    setStatus(Notification.permission === "granted" ? "on" : "off");
  }, []);

  async function toggle() {
    setBusy(true);
    try {
      if (status === "on") {
        await disablePushNotifications();
        setStatus("off");
      } else {
        const result = await enablePushNotifications();
        setStatus(result === "granted" ? "on" : result === "unsupported" ? "unsupported" : "off");
      }
    } finally {
      setBusy(false);
    }
  }

  if (status === "unsupported") {
    return (
      <p className="notification-settings muted">
        Push nudges aren't supported in this browser (on iOS, add this app to your home
        screen first).
      </p>
    );
  }

  return (
    <button type="button" className="notification-toggle" onClick={() => void toggle()} disabled={busy}>
      {status === "on" ? "Nudges on \u2013 tap to disable" : "Enable Critical-item nudges"}
    </button>
  );
}
