/**
 * Which skills a draft contributes to the form.
 *
 * ## Why this is its own module
 *
 * It was three lines inside `applyDraft` in `AddProject.jsx`, which is a
 * 500-line component with a `useForm` in it. There was no way to test it without
 * rendering the whole page, so it was untested -- and it is the step that decides
 * whether the AI's answer reaches the form at all.
 *
 * Extracted so the answer can be checked against the real catalogue rather than
 * asserted.
 *
 * ## The names, not the ids
 *
 * The form holds skill *names* (`skills: ["React", "C#"]`), not ids, because that
 * is what the create endpoint takes and what a chip displays. So the draft's
 * `skill_names` have to be matched against the names the form loaded, and
 * anything unrecognised is dropped: a name the model invented must not be able to
 * make the form un-submittable.
 */

/**
 * @param {string[]|undefined} proposedNames  `skill_names` from the draft.
 * @param {string[]} existing                 Skills already on the form.
 * @param {string[]} catalogueNames           Every name the form loaded.
 * @returns {{merged: string[], added: string[], rejected: string[], catalogueEmpty: boolean}}
 *   `merged` is what the form should hold. `added` is what the draft
 *   contributed. `rejected` is what it proposed that the platform does not have --
 *   surfaced so the client is told, rather than watching a suggestion disappear.
 *   `catalogueEmpty` means there was nothing to check against, which is a
 *   different situation entirely and must not be reported as `rejected`.
 */
export function selectDraftSkills(
  proposedNames,
  existing = [],
  catalogueNames = []
) {
  const catalogue = Array.isArray(catalogueNames) ? catalogueNames : [];
  const known = new Set(catalogue);

  // No catalogue means no way to tell a real skill from an invented one.
  //
  // `skillsData` is `skillsResponse ?? []`, so it is empty before the query
  // resolves and stays empty if the request fails. Treating that as "these skills
  // do not exist" would drop every suggestion and -- worse -- tell the client
  // their skills are not on this platform, which is a claim nothing checked.
  //
  // In practice the agent takes 30-90s and the query resolves in ~300ms, so this
  // is a failure-and-retry path rather than a race. It is handled because a
  // failed request must not produce a confident lie.
  if (known.size === 0) {
    return {
      merged: Array.isArray(existing) ? [...existing] : [],
      added: [],
      rejected: [],
      catalogueEmpty: true,
    };
  }

  const candidates = Array.isArray(proposedNames) ? proposedNames : [];
  const rejected = [];
  const accepted = [];

  for (const raw of candidates) {
    if (typeof raw !== "string") continue;
    const name = raw.trim();
    if (!name) continue;

    if (known.has(name)) {
      if (!accepted.includes(name)) accepted.push(name);
      continue;
    }

    // Case-insensitive rescue, then verify against the catalogue's own spelling.
    //
    // The exact-match-only version dropped a real skill whenever the model's
    // casing differed -- and the model's casing is not something this form
    // controls. "javascript" is the same catalogue row as "JavaScript", and
    // losing it silently is indistinguishable from the skill not existing.
    //
    // The catalogue's spelling wins, so what lands on the form is a name the
    // server can match: `.NET` must not become `.net` and miss.
    const canonical = catalogue.find(
      (candidate) => candidate.toLowerCase() === name.toLowerCase()
    );

    if (canonical === undefined) {
      if (!rejected.includes(name)) rejected.push(name);
      continue;
    }
    if (!accepted.includes(canonical)) accepted.push(canonical);
  }

  const current = Array.isArray(existing) ? existing : [];
  const merged = [...new Set([...current, ...accepted])];

  return {
    merged,
    added: merged.filter((name) => !current.includes(name)),
    rejected,
    catalogueEmpty: false,
  };
}