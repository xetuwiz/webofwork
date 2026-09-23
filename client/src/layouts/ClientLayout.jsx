import { Outlet } from "react-router-dom";
import InfrastructureShell from "../components/InfrastructureShell.jsx";

const navigation = [
  { href: "/", label: "Home" },
  { href: "/find-talents", label: "Find talent" },
  { href: "/my-projects", label: "My projects" },
  { href: "/contracts", label: "Contracts" },
  { href: "/profile", label: "Profile" },
];

export default function ClientLayout() {
  return <InfrastructureShell eyebrow="Marketplace" title="Client workspace" navigation={navigation}><Outlet /></InfrastructureShell>;
}