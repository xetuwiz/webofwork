import { describe, expect, it } from "vitest";
import { validatePaymentForm } from "../paymentForm.js";

describe("payment form", () => {
  it("requires a PayPal order ID and positive amount", () => {
    expect(validatePaymentForm({ paypalOrderId: "", paypalAmountUsd: 0 }).valid).toBe(false);
  });

  it("accepts a valid payment verification payload", () => {
    expect(validatePaymentForm({ paypalOrderId: "PA-123", paypalAmountUsd: 125.5 }).valid).toBe(true);
  });
});
