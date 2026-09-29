/**
 * Remembers an in-flight signup so it survives a closed tab or a crash.
 *
 * Signup is a two-step flow: send-otp caches the form data in Redis under
 * register_temp:{email} for 10 minutes, then verify-otp turns it into a real
 * account. Nothing was stored client-side, so closing the browser at the OTP
 * screen lost the address and forced a full restart -- even though the
 * registration was still alive server-side.
 *
 * We deliberately store ONLY the email. The password is never persisted: it
 * lives in Redis until verification, and duplicating it into localStorage
 * would widen the blast radius of an XSS for no benefit. The name is not
 * needed either, because the server already holds the whole record and
 * resend-otp confirms it is still there.
 */

const STORAGE_KEY = "wow:pending-signup-email";

export function rememberPendingSignup(email) {
  try {
    if (typeof email === "string" && email.includes("@")) {
      window.localStorage.setItem(STORAGE_KEY, email.trim().toLowerCase());
    }
  } catch {
    // Private browsing / storage disabled: resume is a convenience, not a
    // requirement, so failing to store is fine.
  }
}

export function getPendingSignupEmail() {
  try {
    return window.localStorage.getItem(STORAGE_KEY) || null;
  } catch {
    return null;
  }
}

export function clearPendingSignup() {
  try {
    window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Ignore.
  }
}