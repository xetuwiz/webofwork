export const paymentFormSchema = {
  paypalOrderId: (value) => typeof value === "string" && value.trim().length > 0,
  paypalAmountUsd: (value) => Number.isFinite(Number(value)) && Number(value) > 0,
};

export function validatePaymentForm(values) {
  return {
    valid: Object.entries(paymentFormSchema).every(([key, validate]) => validate(values[key])),
    errors: Object.fromEntries(Object.entries(paymentFormSchema).filter(([key, validate]) => !validate(values[key])).map(([key]) => [key, "Invalid payment value."])),
  };
}
