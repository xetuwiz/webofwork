import { NavLink } from "react-router-dom";

const links = [["/admin", "Overview"], ["/admin/users", "Users"], ["/admin/category-management", "Categories"], ["/admin/payment-release", "Payouts"], ["/admin/disputes", "Disputes"], ["/admin/reviews", "Reviews"], ["/admin/settings", "Settings"]];

export default function AdminSidebar() {
  return <aside className="admin-sidebar"><span className="app-shell__eyebrow">WebOfWork</span><strong>Admin workspace</strong><nav>{links.map(([href, label]) => <NavLink key={href} to={href} end={href === "/admin"}>{label}</NavLink>)}</nav></aside>;
}