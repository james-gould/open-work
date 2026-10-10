import { useState, type FormEvent } from "react";
import { api } from "../api";

interface Props {
  onDone: (token: string) => void;
}

/** Shown on a blank install (no PIN configured yet) so the owner can choose one. */
export function PinSetup({ onDone }: Props) {
  const [pin, setPin] = useState("");
  const [confirmPin, setConfirmPin] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (pin !== confirmPin) {
      setError("PINs don't match.");
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      const { token } = await api.setupPin(pin);
      onDone(token);
    } catch {
      setError("Couldn't set the PIN - it must be 4-12 characters.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="auth-screen">
      <h1>Welcome to Open Work</h1>
      <p>This installation has no PIN yet. Choose one (4-12 characters) to lock it.</p>
      <form className="auth-form" onSubmit={handleSubmit}>
        <label>
          PIN
          <input
            type="password"
            inputMode="numeric"
            autoComplete="new-password"
            autoFocus
            value={pin}
            onChange={(e) => setPin(e.target.value)}
          />
        </label>
        <label>
          Confirm PIN
          <input
            type="password"
            inputMode="numeric"
            autoComplete="new-password"
            value={confirmPin}
            onChange={(e) => setConfirmPin(e.target.value)}
          />
        </label>
        {error && <p className="error-banner">{error}</p>}
        <button type="submit" disabled={submitting || !pin || !confirmPin}>
          {submitting ? "Setting up…" : "Set PIN"}
        </button>
      </form>
    </div>
  );
}
