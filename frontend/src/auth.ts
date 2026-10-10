// The JWT lives in localStorage (not sessionStorage/a cookie) so a login survives
// closing the tab/PWA and reopening it later - this is a single-user personal app with no
// third-party scripts running on the page, so the XSS tradeoff vs. an httpOnly cookie is
// acceptable here.
const TOKEN_KEY = "openwork.token";

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

/** Dispatched whenever a request comes back 401, so the app can drop back to the login
 * screen without every caller having to check for this explicitly. */
export const UNAUTHORIZED_EVENT = "openwork:unauthorized";

export function notifyUnauthorized(): void {
  clearToken();
  window.dispatchEvent(new Event(UNAUTHORIZED_EVENT));
}
