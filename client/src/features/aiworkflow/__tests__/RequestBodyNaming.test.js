import { describe, expect, it, vi, beforeEach } from "vitest";

const post = vi.hoisted(() => vi.fn());
const get = vi.hoisted(() => vi.fn());

vi.mock("../../../api/axiosInstance", () => ({
  default: { post, get, put: vi.fn(), patch: vi.fn(), delete: vi.fn() },
}));

const api = await import("../aiWorkflowApi");

/**
 * Do the request bodies bind on the server?
 *
 * ## The failure
 *
 * `POST /ai/workflows/25/approve-shortlist` returned
 *
 *     400 Approving an empty shortlist would end the run without contacting anyone.
 *
 * while the page said **"4 selected"**. Four ids were sent; the service received
 * none.
 *
 * ## Why
 *
 * `Program.cs` sets `PropertyNamingPolicy = SnakeCaseLower`, and that policy
 * governs *deserialization* as well as serialization. `ApproveShortlistRequest`
 * declares `FreelancerIds`, so it binds the JSON key `freelancer_ids`.
 * `PropertyNameCaseInsensitive` defaults to true, but that forgives differences of
 * *case* — an underscore is not a case difference. The client sent `freelancerIds`
 * and the DTO kept its `= new()` initialiser: an empty list, no error at the
 * boundary.
 *
 * Every key in these bodies was single-word except the one that mattered, which is
 * why the payload looked entirely plausible.
 *
 * ## What these tests are for
 *
 * They assert the literal keys on the wire. Asserting that the right *values* were
 * passed — which is what a normal API test does — passes identically against the
 * broken code, because the values really were correct; only the key names were
 * wrong. So each test here checks the serialized body, not the arguments.
 */

function lastBody() {
  expect(post).toHaveBeenCalled();
  return post.mock.calls[post.mock.calls.length - 1][1];
}

describe("approving a shortlist", () => {
  beforeEach(() => {
    post.mockReset();
    post.mockResolvedValue({ data: { invitation_ids: [1, 2] } });
  });

  it("sends the ids under the key the server binds", async () => {
    const ids = [
      "657797ec-07d3-4205-94ed-ea256d7b958a",
      "b944584e-2cd9-43f9-b5c7-a25e2c865ff4",
    ];

    await api.approveShortlist({ id: 25, freelancerIds: ids, rationale: "these" });

    expect(lastBody()).toHaveProperty("freelancer_ids", ids);
  });

  it("does not send a camelCase key, which binds to nothing", async () => {
    await api.approveShortlist({
      id: 25,
      freelancerIds: ["657797ec-07d3-4205-94ed-ea256d7b958a"],
    });

    expect(lastBody()).not.toHaveProperty("freelancerIds");
  });

  it("would not arrive empty even if the ids were", async () => {
    // The exact shape the server rejects. Asserted so the regression is visible as
    // a test failure rather than as a 400 in the user's face.
    await api.approveShortlist({ id: 25, freelancerIds: [] });

    // The key is present and empty, which is honest. The old code sent the ids
    // under a key the server ignored, so an empty selection and four selected
    // requests were indistinguishable on arrival.
    expect(lastBody().freelancer_ids).toEqual([]);
  });

  it("keeps the rationale, which binds either way", async () => {
    await api.approveShortlist({ id: 25, freelancerIds: ["a"], rationale: "why" });

    expect(lastBody().rationale).toBe("why");
  });

  it("targets the run it was given", async () => {
    await api.approveShortlist({ id: 25, freelancerIds: ["a"] });

    expect(post.mock.calls[0][0]).toBe("/ai/workflows/25/approve-shortlist");
  });
});

describe("starting a workflow", () => {
  beforeEach(() => {
    post.mockReset();
    post.mockResolvedValue({ data: { id: 1 } });
  });

  it("sends the project under the key the server binds", async () => {
    await api.startWorkflow({ objective: "find talent", stage: "talent_match", projectId: 11 });

    expect(lastBody()).toHaveProperty("project_id", 11);
  });

  it("does not send a camelCase key", async () => {
    await api.startWorkflow({ objective: "o", stage: "talent_match", projectId: 11 });

    expect(lastBody()).not.toHaveProperty("projectId");
  });

  it("still sends the stage and objective", async () => {
    await api.startWorkflow({ objective: "find talent", stage: "talent_match", projectId: 11 });

    expect(lastBody()).toMatchObject({ objective: "find talent", stage: "talent_match" });
  });
});

describe("the manager gate's contract", () => {
  beforeEach(() => {
    post.mockReset();
    post.mockResolvedValue({ data: { id: 1 } });
  });

  const spec = {
    freelancerId: "657797ec-07d3-4205-94ed-ea256d7b958a",
    totalAmount: "2500.00",
    packageId: "b944584e-2cd9-43f9-b5c7-a25e2c865ff4",
    projectId: 11,
  };

  it("puts the four fields at the request root, where the DTO reads them", async () => {
    // They were nested under `contractSpec`, which binds to nothing. The server
    // then built `contractSpec = null` and approved the run without creating a
    // contract -- the manager gate doing half its job and reporting success.
    await api.approveWorkflow({ id: 7, decision: "approve", contractSpec: spec });

    const body = lastBody();
    expect(body.freelancer_id).toBe(spec.freelancerId);
    expect(body.total_amount).toBe(spec.totalAmount);
    expect(body.package_id).toBe(spec.packageId);
    expect(body.project_id).toBe(spec.projectId);
  });

  it("does not nest them", async () => {
    await api.approveWorkflow({ id: 7, decision: "approve", contractSpec: spec });

    expect(lastBody()).not.toHaveProperty("contractSpec");
  });

  it("omits the keys entirely when no spec was supplied", async () => {
    // Four explicit nulls would read as "a contract with no freelancer", which is
    // not the same statement as "no contract".
    await api.approveWorkflow({ id: 7, decision: "approve" });

    const body = lastBody();
    expect(body).not.toHaveProperty("freelancer_id");
    expect(body).not.toHaveProperty("total_amount");
  });

  it("still carries the decision and rationale", async () => {
    await api.approveWorkflow({ id: 7, decision: "approve", rationale: "looks right" });

    expect(lastBody()).toMatchObject({ decision: "approve", rationale: "looks right" });
  });
});