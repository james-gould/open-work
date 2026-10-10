import { useState, type FormEvent } from "react";
import { api } from "../api";

interface Props {
  onDone: (token: string) => void;
  onForgot?: () => void;
}

export function PinLogin({ onDone, onForgot }: Props) {
  const [pin, setPin] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      const { token } = await api.loginPin(pin);
      onDone(token);
    } catch {
      setError("Incorrect PIN, or locked out after too many attempts - try again shortly.");
    } finally {
      setSubmitting(false);
      setPin("");
    }
  }

  return (
    <div className="auth-screen">
      <h1>Open Work</h1>
      <form className="auth-form" onSubmit={handleSubmit}>
        <label>
          PIN
          <input
            type="password"
            inputMode="numeric"
            autoComplete="current-password"
            autoFocus
            value={pin}
            onChange={(e) => setPin(e.target.value)}
          />
        </label>
        {error && <p className="error-banner">{error}</p>}
        <button type="submit" disabled={submitting || !pin}>
          {submitting ? "Checking…" : "Unlock"}
        </button>
      </form>
      {onForgot && (
        <button type="button" className="link-button" onClick={onForgot}>
          Forgot your PIN?
        </button>
      )}
    </div>
  );
}
