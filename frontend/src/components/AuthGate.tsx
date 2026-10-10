import { useEffect, useState, type ReactNode } from "react";
import { api } from "../api";
import { UNAUTHORIZED_EVENT, clearToken, setToken } from "../auth";
import { ForgotPin } from "./ForgotPin";
import { PinLogin } from "./PinLogin";
import { PinSetup } from "./PinSetup";

type Phase = "checking" | "unreachable" | "setup" | "login" | "forgot" | "authed";

interface Props {
  /** Render-prop so the authenticated app can trigger a logout and see recovery state. */
  children: (ctx: { logout: () => void; hasRecovery: boolean }) => ReactNode;
}

/**
 * Gatekeeper for the whole app: blank install -> PIN setup, configured install -> PIN
 * login (with an optional passkey-recovery escape hatch), either of which mints a token
 * that is then attached to every subsequent API call until it expires or a 401 is seen.
 */
export function AuthGate({ children }: Props) {
  const [phase, setPhase] = useState<Phase>("checking");
  const [hasRecovery, setHasRecovery] = useState(false);

  async function checkStatus() {
    try {
      const status = await api.authStatus();
      setHasRecovery(status.hasRecovery);
      setPhase(status.configured ? "login" : "setup");
    } catch {
      setPhase("unreachable");
    }
  }

  useEffect(() => {
    void checkStatus();
  }, []);

  useEffect(() => {
    function handleUnauthorized() {
      setPhase((p) => (p === "authed" ? "login" : p));
    }

    window.addEventListener(UNAUTHORIZED_EVENT, handleUnauthorized);
    return () => window.removeEventListener(UNAUTHORIZED_EVENT, handleUnauthorized);
  }, []);

  function logout() {
    clearToken();
    setPhase("login");
  }

  if (phase === "checking") {
    return <p className="empty-state">Loading…</p>;
  }

  if (phase === "unreachable") {
    return <p className="error-banner">Couldn't reach the Open Work API.</p>;
  }

  if (phase === "setup") {
    return (
      <PinSetup
        onDone={(token) => {
          setToken(token);
          setPhase("authed");
        }}
      />
    );
  }

  if (phase === "login") {
    return (
      <PinLogin
        onDone={(token) => {
          setToken(token);
          setPhase("authed");
        }}
        onForgot={hasRecovery ? () => setPhase("forgot") : undefined}
      />
    );
  }

  if (phase === "forgot") {
    return (
      <ForgotPin
        onWiped={() => {
          setHasRecovery(false);
          setPhase("setup");
        }}
        onCancel={() => setPhase("login")}
      />
    );
  }

  return <>{children({ logout, hasRecovery })}</>;
}
