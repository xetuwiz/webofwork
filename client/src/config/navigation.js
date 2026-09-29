/**
 * The application's navigation, in one place.
 *
 * ## Why this file exists
 *
 * The three sidebars each declared their own private array -- `menuItems` in the
 * freelancer Sidebar, `navigationItems` in AdminSidebar, `navigation` in
 * ProfileSidebar -- none exported, and all three disagreed on shape: `path` vs
 * `href`, `label` vs `name`. Adding a destination meant editing an array nobody
 * else could see, and a command palette would have had to duplicate all three.
 *
 * So the arrays moved here, normalised to `{ label, path, icon, group, roles }`,
 * and the sidebars import from this module. Adding a destination is now a
 * one-line change and the palette picks it up for free.
 *
 * ## `roles`
 *
 * A destination can belong to more than one role. Entries with no `roles` are
 * public and shown to everyone, including signed-out visitors.
 *
 * Values are the `current_role` strings the API returns -- lowercase
 * `'client'`, `'freelancer'`, `'admin'`. Matched case-insensitively in
 * `filterByRole` because the enum on the server is PascalCase and a mismatch
 * would silently empty the palette.
 *
 * ## Scope
 *
 * Top-level destinations only. Detail routes (`/contracts/5`,
 * `/my-projects/3/proposals`, `/find-talents/9`) are deliberately absent: a
 * palette is for getting somewhere, and flooding it with parametrised paths
 * nobody can predict would hurt it. Parameterised pages are still reachable
 * from their list.
 */
import {
  Home,
  Briefcase,
  FileText,
  MessageSquare,
  CheckSquare,
  Tag,
  LayoutDashboard,
  Users,
  Bell,
  AlertCircle,
  Repeat,
  Wallet,
  Settings,
  User,
  Edit3,
  Sparkles,
  TrendingUp,
  Search,
  PlusCircle,
  LifeBuoy,
  CircleHelp,
  Info,
  ShieldCheck,
} from "lucide-react";

export const NAVIGATION = [
  // ── Client ────────────────────────────────────────────────────────────────
  { label: "Dashboard", path: "/", icon: Home, group: "Client", roles: ["client"] },
  { label: "Find Talent", path: "/find-talents", icon: Search, group: "Client", roles: ["client"] },
  { label: "How It Works", path: "/how-it-works", icon: LifeBuoy, group: "Client", roles: ["client"] },
  { label: "My Posts", path: "/my-projects", icon: FileText, group: "Client", roles: ["client"] },
  { label: "Post a Project", path: "/create-project", icon: PlusCircle, group: "Client", roles: ["client"] },
  { label: "Contracts", path: "/contracts", icon: FileText, group: "Client", roles: ["client"] },

  // ── Client profile ────────────────────────────────────────────────────────
  { label: "Profile Overview", path: "/profile", icon: User, group: "Profile", roles: ["client"] },
  { label: "Edit Profile", path: "/profile/edit", icon: Edit3, group: "Profile", roles: ["client"] },
  { label: "Skills & Interests", path: "/profile/interests", icon: Sparkles, group: "Profile", roles: ["client"] },
  { label: "Account Settings", path: "/profile/settings", icon: Settings, group: "Profile", roles: ["client"] },
  { label: "Spending Summary", path: "/profile/spending", icon: TrendingUp, group: "Profile", roles: ["client"] },
  // Added with the shared notification settings screen. The route existed for a
  // freelancer only, so a client had a settings page that linked to nothing for
  // this and no other way to reach it.
  { label: "Notifications", path: "/settings/notifications", icon: Bell, group: "Profile", roles: ["client"] },

  // ── Freelancer ────────────────────────────────────────────────────────────
  { label: "Dashboard", path: "/freelancer", icon: Home, group: "Freelancer", roles: ["freelancer"] },
  { label: "Find Jobs", path: "/freelancer/jobs", icon: Briefcase, group: "Freelancer", roles: ["freelancer"] },
  { label: "Contracts", path: "/freelancer/contracts", icon: FileText, group: "Freelancer", roles: ["freelancer"] },
  { label: "Proposals", path: "/freelancer/my-proposals", icon: MessageSquare, group: "Freelancer", roles: ["freelancer"] },
  { label: "Invitations", path: "/freelancer/invitations", icon: CheckSquare, group: "Freelancer", roles: ["freelancer"] },
  { label: "Offers", path: "/freelancer/offers", icon: Tag, group: "Freelancer", roles: ["freelancer"] },

  // ── Admin ─────────────────────────────────────────────────────────────────
  { label: "Dashboard", path: "/admin", icon: LayoutDashboard, group: "Admin", roles: ["admin"] },
  { label: "Users", path: "/admin/users", icon: Users, group: "Admin", roles: ["admin"] },
  { label: "Disputes", path: "/admin/disputes", icon: AlertCircle, group: "Admin", roles: ["admin"] },
  { label: "Categories", path: "/admin/category-management", icon: Tag, group: "Admin", roles: ["admin"] },
  { label: "Transactions", path: "/admin/transactions", icon: Repeat, group: "Admin", roles: ["admin"] },
  { label: "Payment Release", path: "/admin/payment-release", icon: Wallet, group: "Admin", roles: ["admin"] },
  { label: "Support", path: "/admin/support", icon: MessageSquare, group: "Admin", roles: ["admin"] },
  { label: "Settings", path: "/admin/settings", icon: Settings, group: "Admin", roles: ["admin"] },
  // Same reason as the client entry: the screen was freelancer-only, so an admin
  // could not manage their own notifications.
  { label: "Notifications", path: "/admin/settings/notifications", icon: Bell, group: "Admin", roles: ["admin"] },

  // ── Public: no `roles`, so shown to everyone ──────────────────────────────
  { label: "About Us", path: "/about-us", icon: Info, group: "Public" },
  { label: "FAQ", path: "/faq", icon: CircleHelp, group: "Public" },
  { label: "Support", path: "/support", icon: LifeBuoy, group: "Public" },
  { label: "Contact Us", path: "/contact-us", icon: MessageSquare, group: "Public" },
  { label: "Terms & Conditions", path: "/terms-and-conditions", icon: ShieldCheck, group: "Public" },
  { label: "Privacy Policy", path: "/privacy-policies", icon: ShieldCheck, group: "Public" },
];

/**
 * The destinations a given user is allowed to reach.
 *
 * @param {string | null | undefined} currentRole `state.auth.user.current_role`.
 *   `null` (signed out) yields the public entries only.
 * @returns {typeof NAVIGATION}
 */
export function filterByRole(currentRole) {
  const role = (currentRole ?? "").toString().trim().toLowerCase();
  if (!role) return NAVIGATION.filter((item) => !item.roles);
  return NAVIGATION.filter((item) => !item.roles || item.roles.some((r) => r.toLowerCase() === role));
}

/**
 * Substring match over label, path and group, so "contr" finds both contract
 * lists and typing a path fragment works.
 *
 * @param {string} query
 * @param {typeof NAVIGATION} items
 */
export function searchNavigation(query, items) {
  const q = query.trim().toLowerCase();
  if (!q) return items;
  return items.filter(
    (item) =>
      item.label.toLowerCase().includes(q) ||
      item.path.toLowerCase().includes(q) ||
      item.group.toLowerCase().includes(q),
  );
}
