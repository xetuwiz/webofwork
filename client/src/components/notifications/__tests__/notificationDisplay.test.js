import { describe, expect, it } from "vitest";
import { formatNotification } from "../notificationDisplay.js";

describe("notification display", () => {
  it("provides safe title, body, and read defaults", () => {
    expect(formatNotification({ id: 1, body: "Saved" })).toEqual({ id: 1, body: "Saved", title: "Update", message: "Saved", isRead: false });
  });
});
