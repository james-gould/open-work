import { useState } from "react";
import { api } from "../api";

interface Props {
  onWiped: () => void;
  onCancel: () => void;
}

/**
 * The "I lost my PIN" escape hatch. Verifies the registered recovery passkey, then - with
 * no gentler alternative - wipes every work item and resets the installation to blank.
 * That tradeoff (strong 2FA or nothing) is deliberate: there is no password-reset email to
 * send anywhere in a single-user, self-hosted app like this one.
 */
export function ForgotPin({ onWiped, onCancel }: Props) {
  const [working, setWorking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleRecover() {
    setWorking(true);
    setError(null);
    try {
      const options = await api.recoveryAssertionOptions();
      const requestOptions = PublicKeyCredential.parseRequestOptionsFromJSON(options);
      const assertion = (await navigator.credentials.get({ publicKey: requestOptions })) as PublicKeyCredential;
      await api.recoveryWipe(assertion.toJSON());
      onWiped();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Recovery failed - the passkey wasn't verified.");
    } finally {
      setWorking(false);
    }
  }

  return (
    <div className="auth-screen">
      <h1>Recover access</h1>
      <p className="danger-text">
        This verifies your recovery passkey, then permanently deletes every work item and
        push subscription and resets this installation back to blank. There is no "keep my
        data" option.
      </p>
      {error && <p className="error-banner">{error}</p>}
      <div className="auth-form-actions">
        <button type="button" onClick={() => void handleRecover()} disabled={working}>
          {working ? "Verifying passkey…" : "Verify passkey and wipe"}
        </button>
        <button type="button" className="link-button" onClick={onCancel} disabled={working}>
          Cancel
        </button>
      </div>
    </div>
  );
}
