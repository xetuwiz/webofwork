/**
 * Dialog size audit.
 *
 * Reports every real `<Dialog>` usage, its declared `size`, and its title, so
 * the size tokens can be checked against the content each one holds.
 *
 * ## Why this is a script and not a grep
 *
 * The first attempt at this was a regex over the raw file text and it produced
 * 50 "modals", most of them nonsense: matches inside block comments (the
 * migration notes in PrivacyPolicy, TermAndConditions, AdminSettings and
 * ContractDetail all *describe* the old hand-rolled scrim in prose that contains
 * the literal text), a match inside `Dialog.jsx`'s own documentation, and a
 * match in `Popover.jsx`. 30 of the 50 also reported `title=(none)`, which was
 * the tell -- a Dialog cannot render without a title, so those were not Dialogs
 * at all.
 *
 * So: comments are stripped first, and an actual `title` prop is required for a
 * match to count. That is the same lesson as `label-audit.mjs` -- the first
 * version of a text-matching tool in this repo was wrong by an order of
 * magnitude, twice, in two different files.
 */
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";

const root = resolve(process.argv[2] ?? "src");

/** Size token -> px, mirroring the --dialog-* custom properties in index.css. */
const SIZES = { sm: 448, md: 600, lg: 720, xl: 1100 };

/** Same comment stripper as label-audit.mjs; kept in sync deliberately. */
function stripComments(source) {
  let out = "";
  let i = 0;
  const n = source.length;
  while (i < n) {
    const two = source.slice(i, i + 2);
    if (two === "/*" && /[A-Za-z0-9_$]/.test(source[i - 1] ?? "")) {
      out += source[i++];
    } else if (two === "/*") {
      const end = source.indexOf("*/", i + 2);
      const stop = end === -1 ? n : end + 2;
      out += source.slice(i, stop).replace(/[^\n]/g, " ");
      i = stop;
    } else if (two === "//") {
      const end = source.indexOf("\n", i);
      const stop = end === -1 ? n : end;
      out += source.slice(i, stop).replace(/[^\n]/g, " ");
      i = stop;
    } else {
      out += source[i++];
    }
  }
  return out;
}

function walk(dir) {
  const found = [];
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) found.push(...walk(full));
    else if (/\.jsx?$/.test(entry)) found.push(full);
  }
  return found;
}

const rows = [];

for (const file of walk(root)) {
  const rel = relative(root, file).replace(/\\/g, "/");
  // The primitive itself documents its own markup; skip it.
  if (rel === "components/common/Dialog.jsx") continue;

  const src = stripComments(readFileSync(file, "utf8"));

  for (const m of src.matchAll(/<Dialog\b/g)) {
    // Props run to the matching `>`, allowing `>` inside {...} expressions.
    let i = m.index + m[0].length;
    let depth = 0;
    let end = -1;
    for (; i < src.length; i++) {
      const c = src[i];
      if (c === "{") depth++;
      else if (c === "}") depth--;
      else if (c === ">" && depth === 0) {
        end = i;
        break;
      }
    }
    if (end === -1) continue;
    const tag = src.slice(m.index, end + 1);

    // A Dialog without a title cannot render -- `title` is a required prop and
    // the primitive wires aria-labelledby from it. So its absence means this was
    // never a Dialog element and the match should be discarded.
    const titleMatch = tag.match(/\btitle="([^"]*)"/) ?? tag.match(/\btitle=\{([^}]*)\}/);
    if (!titleMatch) continue;

    const sizeMatch = tag.match(/\bsize="([^"]*)"/);
    const size = sizeMatch ? sizeMatch[1] : "md";
    const title = titleMatch[1].trim();

    const line = src.slice(0, m.index).split("\n").length;
    rows.push({
      size,
      px: SIZES[size] ?? SIZES.md,
      explicit: Boolean(sizeMatch),
      title,
      desc: /\bdescription=/.test(tag),
      footer: /\bfooter=/.test(tag),
      file: rel,
      line,
    });
  }
}

const order = ["sm", "md", "lg", "xl"];
rows.sort((a, b) => order.indexOf(a.size) - order.indexOf(b.size) || a.title.localeCompare(b.title));

console.log(`  ${rows.length} real <Dialog> usages\n`);
for (const size of order) {
  const group = rows.filter((r) => r.size === size);
  if (!group.length) continue;
  console.log(`  ${size}  (${SIZES[size]}px)  --  ${group.length}`);
  for (const r of group) {
    const flags = [r.explicit ? "" : "default", r.desc ? "desc" : "", r.footer ? "footer" : ""]
      .filter(Boolean)
      .join(",");
    console.log(`     ${r.title.padEnd(38).slice(0, 38)}  ${flags.padEnd(14)}  ${r.file}:${r.line}`);
  }
  console.log("");
}
