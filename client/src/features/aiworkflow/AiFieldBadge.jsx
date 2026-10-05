import React from "react";
import { Sparkles, Pencil } from "lucide-react";

/**
 * Marks a field the AI filled, and stops marking it the moment it is edited.
 *
 * ## Why the badge goes away
 *
 * A badge that says "AI-generated" is a claim about the current contents of that
 * field. Once the client types in it, the contents are theirs, and leaving the
 * badge would be asserting something untrue -- and worse, it would suggest they
 * had not looked at it.
 *
 * So this renders only while the field is both AI-filled *and* untouched. The
 * parent decides that with react-hook-form's `isDirty`, and `applyDraft` fills
 * fields with `shouldDirty: false` so that typing -- and only typing -- changes
 * the answer. No manual tracking, so it cannot drift out of step with the form.
 *
 * ## Not a dismiss button
 *
 * There is no "accept" to click, because there is nothing to accept. The draft
 * has already been written into the form; the client can edit, ignore, or delete
 * every field before posting, and posting is a separate deliberate act they take
 * themselves. A badge with an Accept button would imply a pending state that
 * does not exist.
 */
export default function AiFieldBadge({ show, className = "" }) {
  if (!show) return null;

  return (
    <p
      className={`flex items-center gap-1.5 text-[11px] text-brand-fg ${className}`}
      // Announced rather than purely visual: a screen-reader user filling this
      // form has no other way to learn the starting content was not their own.
      role="status"
    >
      <Sparkles size={12} aria-hidden="true" className="shrink-0" />
      Filled in by the AI. Change anything you disagree with.
    </p>
  );
}

/**
 * The state after a client has edited an AI-filled field.
 *
 * Kept as a separate component so the transition is one place, and so a field
 * the client has since rewritten says so plainly rather than going back to
 * looking untouched.
 */
export function AiFieldEdited({ show, className = "" }) {
  if (!show) return null;

  return (
    <p
      className={`flex items-center gap-1.5 text-[11px] text-fg-subtle ${className}`}
      role="status"
    >
      <Pencil size={12} aria-hidden="true" className="shrink-0" />
      Edited from the AI&apos;s suggestion.
    </p>
  );
}
