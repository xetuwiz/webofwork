import { Outlet } from "react-router-dom";
import InfrastructureShell from "../components/InfrastructureShell.jsx";

const navigation = [
  { href: "/profile", label: "Overview" },
  { href: "/profile/settings", label: "Account settings" },
  { href: "/profile/interests", label: "Interests" },
  { href: "/profile/edit", label: "Edit profile" },
  { href: "/profile/spending", label: "Spending" },
];

export default function ClientProfileLayout() {
  return <InfrastructureShell eyebrow="Account" title="Client profile" navigation={navigation}><Outlet /></InfrastructureShell>;
}