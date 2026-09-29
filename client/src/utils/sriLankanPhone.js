/**
 * Sri Lankan mobile number helpers.
 *
 * Mirrors server/WebOfWork.Api/Integrations/Sms/SriLankanPhone.cs exactly, so
 * the form never accepts something the API will reject. Both sides accept
 * every one of these and store canonical E.164 (+947XXXXXXXX):
 *
 *   +94779091086  94779091086  0094779091086  0779091086
 *   077 909 1086  077-909-1086  (077) 909 1086  779091086
 *
 * Only allocated mobile operators are accepted: 070, 071, 072, 074, 075,
 * 076, 077, 078. Landlines and the unallocated 073 / 079 ranges are rejected.
 */

const ALLOCATED_OPERATORS = ["070", "071", "072", "074", "075", "076", "077", "078"];

export const PHONE_ERROR_MESSAGE =
  "Enter a valid Sri Lankan mobile number, e.g. 0779091086 or +94779091086.";

const LOCAL_LENGTH = 10; // 07XXXXXXXX
const NATIONAL_LENGTH = 9; // 7XXXXXXXX

/**
 * Strip formatting and return the canonical +94 form.
 * Returns null when the number is not a valid Sri Lankan mobile.
 */
export function normalizeSriLankanPhone(input) {
  if (typeof input !== "string") return null;

  // Everything that is not a digit goes: spaces, dashes, dots, brackets, '+'.
  let digits = input.replace(/\D/g, "");

  // International access code: 0094...
  if (digits.startsWith("0094")) digits = digits.slice(2);

  // Bare country code: 94XXXXXXXXX
  if (digits.length === NATIONAL_LENGTH + 2 && digits.startsWith("94")) {
    digits = digits.slice(2);
  }

  // Trunk zero: 07XXXXXXXX -> 7XXXXXXXX
  if (digits.length === LOCAL_LENGTH && digits[0] === "0") {
    digits = digits.slice(1);
  }

  if (digits.length !== NATIONAL_LENGTH || !digits.startsWith("7")) return null;

  // digits[0] is the mobile marker (7); digits[1] is the operator digit.
  const operator = `0${digits.slice(0, 2)}`;
  if (!ALLOCATED_OPERATORS.includes(operator)) return null;

  return `+94${digits}`;
}

/** True when the input is a valid Sri Lankan mobile number. */
export function isValidSriLankanPhone(input) {
  return normalizeSriLankanPhone(input) !== null;
}

/** As-you-type display formatting: 077 909 1086 / +94 77 909 1086. */
export function formatSriLankanPhone(input) {
  const raw = typeof input === "string" ? input.replace(/\D/g, "") : "";
  if (!raw) return "";

  if (raw.startsWith("0094") || (raw.length === 11 && raw.startsWith("94"))) {
    const rest = raw.startsWith("0094") ? raw.slice(4) : raw.slice(2);
    return `+94 ${rest.slice(0, 2)} ${rest.slice(2, 5)} ${rest.slice(5, 9)}`.trim();
  }

  const local = raw.length === LOCAL_LENGTH ? raw : `0${raw}`;
  return `${local.slice(0, 3)} ${local.slice(3, 6)} ${local.slice(6, 10)}`.trim();
}