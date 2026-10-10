import { useState } from "react";
import { api } from "../api";

interface Props {
  hasRecovery: boolean;
  onRegistered: () => void;
}

/** Lets the already-logged-in owner register the passkey that the "forgot PIN" flow
 * will later demand before it wipes the database. */
export function RecoverySettings({ hasRecovery, onRegistered }: Props) {
  const [working, setWorking] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleRegister() {
    setWorking(true);
    setError(null);
    try {
      const options = await api.recoveryRegisterOptions();
      const creationOptions = PublicKeyCredential.parseCreationOptionsFromJSON(options);
      const credential = (await navigator.credentials.create({ publicKey: creationOptions })) as PublicKeyCredential;
      await api.recoveryRegister(credential.toJSON());
      onRegistered();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Couldn't register the passkey.");
    } finally {
      setWorking(false);
    }
  }

  return (
    <div className="recovery-settings">
      <button type="button" onClick={() => void handleRegister()} disabled={working}>
        {working ? "Registering…" : hasRecovery ? "Replace recovery passkey" : "Set up a recovery passkey"}
      </button>
      {!hasRecovery && (
        <p className="muted">
          Without one, a lost PIN can't be recovered - wiping the installation would be the
          only option left.
        </p>
      )}
      {error && <p className="error-banner">{error}</p>}
    </div>
  );
}
