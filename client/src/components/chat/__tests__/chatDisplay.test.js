import { describe, expect, it } from "vitest";
import { formatChatMessage } from "../chatDisplay.js";

describe("chat message display", () => {
  it("marks the current user's message and formats its time", () => {
    expect(formatChatMessage({ content: "Hello", senderId: "u1", createdAt: "2026-01-01T00:00:00Z" }, "u1")).toMatchObject({ isMine: true, content: "Hello" });
  });
  it("ignores empty messages", () => expect(formatChatMessage({ content: " " }, "u1")).toBeNull());
});
