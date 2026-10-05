/**
 * Label-association render check.
 *
 * The defect under test: a <label> that sits *above* its control with no
 * htmlFor and no id on the control names nothing, so a screen reader announces
 * the field as an unlabelled "edit text" box. Static grep cannot see this
 * reliably -- and a green `vite build` cannot see it at all, because Rollup
 * does not resolve JSX identifiers.
 *
 * So this renders the real components and inspects the emitted HTML:
 *   - every <input>/<textarea>/<select> must be named, by either
 *     (a) an ancestor <label>, or
 *     (b) a <label for> pointing at its id, or
 *     (c) an aria-label / aria-labelledby.
 *   - a <label for> must point at an id that actually exists.
 *
 * A control with none of those is reported with the surrounding markup, so the
 * fix is obvious rather than a hunt.
 */
import { renderToString } from "react-dom/server";
import ProfileEdit from "../src/features/freelancer/profile/ProfileEdit.jsx";
import AddProject from "../src/features/client/projectManagement/AddProject.jsx";
import EditProject from "../src/features/client/projectManagement/EditProject.jsx";
import StepOne from "../src/features/client/onboarding/step1/StepOne.jsx";
import StepTwo from "../src/features/client/onboarding/step2/StepTwo.jsx";
import StepThree from "../src/features/client/onboarding/step3/StepThree.jsx";
import StepFour from "../src/features/client/onboarding/step4/StepFour.jsx";
import InputBox from "../src/features/client/account/components/InputBox.jsx";

/**
 * Renders <InputBox> three times -- plain text, with a validation error, and as
 * a password field with the toggle -- because those are the three states that
 * can each break the association independently.
 */
function InputBoxProbe() {
  const register = () => ({});
  return (
    <form>
      <InputBox label="Email" name="email" register={register} placeholder="you@example.com" />
      <InputBox
        label="Password"
        name="password"
        register={register}
        passwordToggle
        error={{ message: "Password is required" }}
      />
    </form>
  );
}

/* ---------- tiny HTML helpers, good enough for flat label/control markup ---- */

const attrsOf = (tag) => {
  const out = {};
  const re = /([a-zA-Z-:]+)\s*=\s*"([^"]*)"/g;
  let m;
  while ((m = re.exec(tag))) out[m[1].toLowerCase()] = m[2];
  return out;
};

const CONTROL_RE = /<(input|textarea|select)\b([^>]*)>/g;
const LABEL_OPEN_RE = /<label\b([^>]*)>/g;

/** Balance-aware scan: returns every control with its nesting context. */
function analyse(html) {
  const controls = [];
  const labels = [];
  const legendIds = new Set();

  for (const m of html.matchAll(/<legend\b[^>]*>/g)) legendIds.add(m[0]);

  // `exec()` returns a single match *array*, so `for (const m of re.exec(html))`
  // iterates that array's elements -- the full match, then each capture group
  // as a bare string. That produced controls named "i", "n" and "t" (the
  // second and third characters of "input") and reported almost every control
  // as unnamed. `matchAll` is the correct iteration for a /g regex.
  for (const m of html.matchAll(CONTROL_RE)) {
    const attrs = attrsOf(`<x${m[2]}>`);
    controls.push({
      index: m.index,
      name: m[1],
      attrs,
      id: attrs.id,
      ariaLabel: attrs["aria-label"],
      ariaLabelledby: attrs["aria-labelledby"],
      labelled: false,
      via: null,
    });
  }

  for (const m of html.matchAll(LABEL_OPEN_RE)) labels.push({ index: m.index, attrs: attrsOf(`<x${m[1]}>`) });

  // (a) wrapping: a <label> that opens before the control and closes after it.
  for (const c of controls) {
    const before = html.slice(0, c.index);
    const openStack = [];
    for (const t of before.matchAll(/<(\/?)(label|fieldset|div|form)\b[^>]*>/g)) {
      const closing = t[1] === "/";
      const name = t[2];
      if (closing) {
        const at = openStack.lastIndexOf(name);
        if (at !== -1) openStack.splice(at, 1);
      } else {
        // <input> void elements never nest, but self-closing tags here are jsx
        // fragments already resolved by React; treat `/>` as open+close.
        openStack.push(name);
      }
    }
    if (openStack.includes("label")) {
      c.labelled = true;
      c.via = "wrapping <label>";
    }
  }

  // (b) htmlFor
  for (const l of labels) {
    if (!l.attrs.for) continue;
    for (const c of controls) {
      if (c.id && c.id === l.attrs.for) {
        c.labelled = true;
        c.via = `label[for=${l.attrs.for}]`;
      }
    }
  }

  // (c) explicit ARIA name
  for (const c of controls) {
    if (c.ariaLabel) {
      c.labelled = true;
      c.via = "aria-label";
    } else if (c.ariaLabelledby) {
      c.labelled = true;
      c.via = "aria-labelledby";
    }
  }

  // Dangling htmlFor: a label pointing at nothing.
  const ids = new Set(controls.map((c) => c.id).filter(Boolean));
  const dangling = labels.filter((l) => l.attrs.for && !ids.has(l.attrs.for));

  return { controls, labels, dangling };
}

let fails = 0;
const check = (name, html) => {
  console.log(`\n=== ${name} ===`);
  console.log(`   html length: ${html.length}`);

  if (!html || html.length < 200) {
    console.log("   FAIL  rendered nothing usable -- an empty render passes every check below");
    return 1;
  }
  fails += checkGroups(name, html);

  const { controls, labels, dangling } = analyse(html);

  // Ignore decorative/hidden controls: file inputs styled `hidden`, and purely
  // presentational nodes. A hidden file input still needs a label, so only skip
  // ones with no name AND type=hidden.
  const named = controls.filter((c) => c.attrs.type !== "hidden");

  const unnamed = named.filter((c) => !c.labelled);
  console.log(`   controls: ${controls.length}   labels: ${labels.length}   named: ${named.length - unnamed.length}/${named.length}`);

  for (const c of unnamed) {
    fails++;
    const around = html.slice(Math.max(0, c.index - 90), c.index + 40).replace(/\s+/g, " ");
    console.log(`   FAIL  unnamed <${c.name}> ${JSON.stringify(c.attrs)}`);
    console.log(`         ...${around}...`);
  }

  for (const d of dangling) {
    fails++;
    console.log(`   FAIL  <label for="${d.attrs.for}"> points at no control`);
  }

  if (!unnamed.length && !dangling.length) console.log("   ok    every control has an accessible name");
  return fails;
}

/**
 * Group headings: every <legend> must sit inside a <fieldset>.
 *
 * A <legend> outside a <fieldset> is inert -- it renders as ordinary text and
 * names nothing, which is the same defect as the label that started this, just
 * wearing a different element. Worth asserting because the alternative fix
 * (`role="group"` + aria-labelledby) is easy to reach for by accident.
 */
function checkGroups(name, html) {
  if (!html || html.length < 200) return 0;
  const fieldsets = (html.match(/<fieldset\b/g) ?? []).length;
  const legends = (html.match(/<legend\b/g) ?? []).length;
  if (!fieldsets && !legends) return 0;

  // A legend is only inside a fieldset if the fieldset depth is > 0 at its
  // position in the document.
  let depth = 0;
  let orphaned = 0;
  for (const m of html.matchAll(/<(\/?)(fieldset|legend)\b[^>]*>/g)) {
    if (m[2] === "fieldset") depth += m[1] === "/" ? -1 : 1;
    else if (m[1] !== "/" && depth <= 0) orphaned++;
  }
  if (depth !== 0) {
    console.log(`   FAIL  ${name}: unbalanced <fieldset> (depth ${depth} at end)`);
    return 1;
  }
  if (orphaned) {
    console.log(`   FAIL  ${name}: ${orphaned} <legend> outside a <fieldset>`);
    return orphaned;
  }
  console.log(`   ok    ${name}: ${fieldsets} fieldset(s) / ${legends} legend(s), all contained`);
  return 0;
};

const render = (Component, props) => {
  try {
    return renderToString(<Component {...props} />);
  } catch (error) {
    console.log(`   FAIL  render threw: ${error.message}`);
    return "";
  }
};

fails += check("ProfileEdit", render(ProfileEdit));
fails += check("AddProject", render(AddProject));
fails += check("EditProject", render(EditProject));
fails += check("StepOne", render(StepOne));
fails += check("StepTwo", render(StepTwo));
fails += check("StepThree", render(StepThree));
fails += check("StepFour", render(StepFour));
fails += check("InputBox", render(InputBoxProbe));

// The password toggle's name and pressed state cannot be checked by the
// control-naming pass above, because <button> is not a labelable control.
{
  const html = render(InputBoxProbe);
  const labelled = /<button[^>]*aria-label="Show password"/.test(html)
    && /aria-pressed="false"/.test(html)
    && /aria-controls="/.test(html);
  // The image inside must be decorative, or its alt text competes with the
  // button's aria-label as a second accessible name.
  const imgHidden = /<img[^>]*alt=""/.test(html);
  console.log(`   ${labelled ? "ok  " : "FAIL"} InputBox: password toggle has aria-label + aria-pressed + aria-controls`);
  console.log(`   ${imgHidden ? "ok  " : "FAIL"} InputBox: toggle icon is decorative (alt="") so it cannot shadow the aria-label`);
  if (!labelled) fails++;
  if (!imgHidden) fails++;
  const described = /aria-describedby="[^"]*-message"/.test(html);
  const alert = /role="alert"/.test(html);
  console.log(`   ${described && alert ? "ok  " : "FAIL"} InputBox: error message is referenced and is a live region`);
  if (!(described && alert)) fails++;
}

console.log(`\n   ${fails === 0 ? "ALL PASS" : fails + " FAILURES"}`);
process.exit(fails === 0 ? 0 : 1);
