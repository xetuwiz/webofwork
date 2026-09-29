/**
 * Detector: <label> elements that associate with no control.
 *
 * A <label> names a control two ways, and only two:
 *   1. `htmlFor` pointing at the control's id, or
 *   2. by *wrapping* it.
 * Anything else -- a label closed above its input, a label heading a group of
 * two or more inputs -- names nothing, and a screen reader announces the
 * control as an unlabelled "edit text" box.
 *
 * Why this exists as a file: the counts were being read off ad-hoc greps that
 * matched (a) JSX *props* named `title`, and (b) the word `<label>` inside
 * comments and prose. Both inflated the number badly -- 69 real defects became
 * 3, and 25 became 22. Comments are stripped before scanning so a comment
 * explaining the defect is not itself counted as one.
 *
 * ## What it cannot see
 *
 * Only `sibling` and `group` are trustworthy. `review` is an explicit "I don't
 * know", not a verdict -- see the note on `.map()` in the classification below.
 *
 * This is also single-line-oriented. A multi-line `<label\n  htmlFor="x">`
 * still parses, but a defect that needs *finding the id* rather than the label
 * is out of scope: a `htmlFor` pointing at an id that does not exist looks
 * correct to any label-shaped grep. Those are caught by
 * `ssr-check/entry.jsx`, which asserts on rendered HTML, and by the static
 * `htmlFor`/id cross-check in the same file.
 *
 * Usage:  node scripts/label-audit.mjs [srcDir]
 */
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, resolve } from "node:path";

const root = resolve(process.argv[2] ?? "src");

/** Remove block and line comments, preserving newlines so line numbers hold. */
function stripComments(source) {
  let out = "";
  let i = 0;
  const n = source.length;
  while (i < n) {
    const two = source.slice(i, i + 2);
    if (two === "/*" && /[A-Za-z0-9_$]/.test(source[i - 1] ?? "")) {
      // Not a comment. This is `image/*` from a file input's accept attribute,
      // where the `/` of `/*` is the slash in `image/` and the character before
      // it is the `e`.
      //
      // This matters more than it looks. That string has no matching close, so
      // a naive stripper blanks to end-of-file -- which is how an earlier run
      // of this script reported "0 defects across the whole app" while
      // ProfileEdit.jsx plainly had eight. It also means a `/*` is only a real
      // comment opener when it is NOT identifier-adjacent, since a genuine
      // block comment follows whitespace, `{`, `=`, or a line break.
      out += source[i++];
    } else if (two === "/*") {
      const end = source.indexOf("*/", i + 2);
      const stop = end === -1 ? n : end + 2;
      // Keep the newlines so reported line numbers still point at the right line.
      out += source.slice(i, stop).replace(/[^\n]/g, " ");
      i = stop;
    } else if (two === "//") {
      const end = source.indexOf("\n", i);
      const stop = end === -1 ? n : end;
      out += source.slice(i, stop).replace(/[^\n]/g, " ");
      i = stop;
    } else {
      // Strings are deliberately NOT tracked. JSX text is full of apostrophes
      // ("freelancer's rate", "don't"), and treating a quote as a string
      // opener swallows everything up to the next quote -- which silently made
      // this scanner report zero defects across the whole app. The only thing
      // lost by ignoring strings is a `//` inside one (e.g. a URL), which eats
      // the rest of that single line -- a far smaller and more visible error
      // than scanning nothing at all.
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

const CONTROL = /<(input|textarea|select)\b/;
const VOID = new Set(["input", "br", "hr", "img", "source", "area", "col"]);

/** The markup inside the first `<name>` element found at the start of `src`. */
function extentOf(src, name) {
  const open = new RegExp(`<${name}\\b[^>]*>`, "i").exec(src);
  if (!open) return "";
  if (VOID.has(name.toLowerCase()) || /\/>$/.test(open[0])) return "";
  const openRe = new RegExp(`<${name}\\b[^>]*>`, "gi");
  const closeRe = new RegExp(`</${name}\\s*>`, "gi");
  let depth = 1;
  let cursor = open.index + open[0].length;
  while (depth > 0) {
    openRe.lastIndex = cursor;
    closeRe.lastIndex = cursor;
    const nextOpen = openRe.exec(src);
    const nextClose = closeRe.exec(src);
    if (!nextClose) return src.slice(open.index + open[0].length); // unbalanced
    if (nextOpen && nextOpen.index < nextClose.index) {
      depth++;
      cursor = nextOpen.index + nextOpen[0].length;
    } else {
      depth--;
      cursor = nextClose.index + nextClose[0].length;
      if (depth === 0) return src.slice(open.index + open[0].length, nextClose.index);
    }
  }
  return "";
}

const rows = [];

for (const file of walk(root)) {
  const raw = readFileSync(file, "utf8");
  const src = stripComments(raw);
  const labels = [...src.matchAll(/<label\b[^>]*>/gs)];
  if (labels.length === 0) continue;

  const sibling = []; // label closed before its control
  const group = []; //   label heading 2+ controls
  const single = []; //  label that correctly wraps its control
  const review = []; //  heading over controls rendered by .map() -- unclassifiable statically

  for (const m of labels) {
    const tag = m[0];
    if (/\bhtmlFor\s*=|\bfor\s*=/.test(tag)) continue; // associated by id

    const line = src.slice(0, m.index).split("\n").length;
    const after = src.slice(m.index + tag.length);
    const close = after.indexOf("</label>");
    if (close === -1) continue; // unterminated; not a complete element

    const inner = after.slice(0, close);
    const controls = (inner.match(new RegExp(CONTROL.source, "g")) ?? []).length;
    // Skip *past* `</label>`. Slicing from `close` instead starts `rest` on
    // the closing tag, so `rest.indexOf(">")` finds the `>` of `</label>` and
    // every label gets classified against the string "/label>". That bug made
    // the whole scan report zero.
    const rest0 = after.slice(close + "</label>".length);
    // Whitespace first. `</label>` is almost always followed by a newline and
    // indentation, so anchoring `^<` against the raw remainder never matched
    // and `nextName` came back undefined for every label in the app -- which is
    // how whole files quietly dropped off the report.
    const rest = rest0.replace(/^\s+/, "");

    if (controls > 0) {
      // Wrapped. A label holding exactly one control is the correct pattern.
      single.push(line);
      continue;
    }

    // Nothing inside, and no htmlFor -- so it can only be associated by id, and
    // it has none. What decides the class is how many controls the *next*
    // element holds, which means counting inside that element's own extent
    // rather than just looking at its tag name.
    //
    // The earlier version classified on tag name alone, so a heading followed
    // by a <div> containing exactly one input was called a "group". StepOne's
    // "Phone Number" is that case: one <input> plus a Verify <button>, and
    // buttons are not labelable controls. Getting this wrong on 20 group labels
    // would have meant adding a fieldset where a plain Field was correct.
    const gt = rest.indexOf(">");
    if (gt === -1) {
      single.push(line);
      continue;
    }
    const nextTag = rest.slice(0, gt + 1);
    const nextName = (nextTag.match(/^<([a-zA-Z][\w-]*)/) ?? [])[1];

    if (CONTROL.test(nextTag)) {
      // Label closed, control immediately follows: the sibling defect.
      sibling.push(line);
      continue;
    }

    if (nextName) {
      const inner2 = extentOf(rest, nextName);
      // Two different counts, because they answer different questions.
      //
      // `labelable` decides sibling-vs-group: a heading over exactly one input
      // is a Field, over two or more it is a fieldset. Counting *all*
      // interactive children instead misfiles StepOne's "Phone Number", which
      // wraps one input plus a Verify button -- the button is a submit action,
      // not a second choice in the group.
      const labelable = (inner2.match(new RegExp(CONTROL.source, "g")) ?? []).length;

      if (labelable >= 2) group.push(line);
      else if (labelable === 1) sibling.push(line);
      // labelable === 0 means the heading sits over controls rendered by
      // `.map()`, where one literal `<button>` in the source becomes N at
      // runtime. Static counting cannot see that: StepTwo's skills and
      // experience-level pickers each look like a single button here.
      //
      // So these are not classified. They are reported as `review` and settled
      // by reading the JSX, with the render harness as ground truth. Guessing
      // here is how this file reported 0 group labels three separate times.
      else review.push(line);
    } else {
      single.push(line);
    }
  }

  if (sibling.length || group.length || review.length) {
    rows.push({
      file: relative(root, file).replace(/\\/g, "/"),
      sibling: sibling.length,
      group: group.length,
      review: review.length,
      lines: [...sibling, ...group, ...review].sort((a, b) => a - b),
    });
  }
}

rows.sort((a, b) => b.sibling + b.group - (a.sibling + a.group));

let tS = 0;
let tG = 0;
let tR = 0;
console.log("  n  sib  grp  file");
for (const r of rows) {
  tS += r.sibling;
  tG += r.group;
  console.log(
    `  ${String(r.sibling + r.group + r.review).padStart(2)}  ${String(r.sibling).padStart(3)}  ${String(r.group).padStart(3)}  ${String(r.review).padStart(3)}  ${r.file}`,
  );
  console.log(`        lines: ${r.lines.join(", ")}`);
}
console.log(
  `\n  ${rows.length} files, ${tS} sibling labels (name nothing), ${tG} group labels (should be fieldset/legend)`,
);
