import z from "zod";
import {
  isValidSriLankanPhone,
  PHONE_ERROR_MESSAGE,
} from "../../../../utils/sriLankanPhone";

export const stepOneSchema = z.object({
  tagline: z.string().min(10, "Tagline must be at least 10 characters").max(80, "Tagline is too long"),
  bio: z.string().min(50, "Bio should be at least 50 characters to attract clients").max(500, "Bio cannot exceed 500 characters"),
  // Was an Indian mobile regex -- [6-9] then 9 digits, with the message
  // "Enter a valid 10-digit Indian mobile number". That is a the reference leftover
  // and it rejected every Sri Lankan number, because "0779091086" starts
  // with 0. The platform only supports Sri Lankan mobiles; every accepted
  // spelling normalizes to +947XXXXXXXX (see utils/sriLankanPhone.js).
  phone: z.string().min(1, "Phone number is required").refine(isValidSriLankanPhone, PHONE_ERROR_MESSAGE),
  agreedToTerms: z.boolean().refine((val) => val === true, "You must agree to the terms to continue"),
});