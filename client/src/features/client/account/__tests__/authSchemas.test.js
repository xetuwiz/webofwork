import { describe, expect, it } from "vitest";
import { signinSchema } from "../validation/authSchemas.js";

describe("signinSchema", () => {
  it("accepts valid credentials", () => {
    const result = signinSchema.safeParse({
      email: "user@example.com",
      password: "Str0ng!Pass",
    });

    expect(result.success).toBe(true);
  });

  it("rejects invalid email", () => {
    const result = signinSchema.safeParse({
      email: "not-an-email",
      password: "Str0ng!Pass",
    });

    expect(result.success).toBe(false);
    expect(result.error.issues[0].path).toEqual(["email"]);
  });

  it("rejects empty password", () => {
    const result = signinSchema.safeParse({
      email: "user@example.com",
      password: "",
    });

    expect(result.success).toBe(false);
    expect(result.error.issues[0].path).toEqual(["password"]);
  });
});
