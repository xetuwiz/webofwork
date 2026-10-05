/**
 * Social links rendered in the site footer.
 *
 * Intentionally empty.
 *
 * The footer previously hard-coded two "Follow us" buttons whose hrefs pointed
 * at one individual's personal X and LinkedIn accounts, while the accessible
 * names said "Follow us" -- so the product shipped a link to a private profile
 * and presented it as the company's own.
 *
 * The buttons were removed rather than repointed at invented handles: a
 * plausible-looking but unregistered URL is worse than no link, because it
 * renders as working and 404s for every visitor.
 *
 * To add real accounts, append to this array:
 *
 *   import { FaXTwitter, FaLinkedinIn } from "react-icons/fa6";
 *
 *   export const SOCIAL_LINKS = [
 *     { label: "X",            href: "https://x.com/<handle>",     icon: FaXTwitter },
 *     { label: "LinkedIn",     href: "https://linkedin.com/company/<slug>", icon: FaLinkedinIn },
 *   ];
 *
 * `ClientFooter` maps over whatever is here and renders nothing while the array
 * is empty, so the container collapses instead of leaving a gap in the layout.
 */
export const SOCIAL_LINKS = [];
