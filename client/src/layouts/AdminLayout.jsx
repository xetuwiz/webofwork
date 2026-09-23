import { Outlet } from "react-router-dom";
import InfrastructureShell from "../components/InfrastructureShell.jsx";

const navigation = [
  { href: "/admin", label: "Overview" },
  { href: "/admin/users", label: "Users" },
  { href: "/admin/category-management", label: "Categories" },
  { href: "/admin/disputes", label: "Disputes" },
  { href: "/admin/settings", label: "Settings" },
];

export default function AdminLayout() {
  return <InfrastructureShell eyebrow="Operations" title="Admin workspace" navigation={navigation}><Outlet /></InfrastructureShell>;
}