import { Outlet } from "react-router-dom";
import InfrastructureShell from "../components/InfrastructureShell.jsx";

const navigation = [
  { href: "/freelancer", label: "Dashboard" },
  { href: "/freelancer/jobs", label: "Find jobs" },
  { href: "/freelancer/my-proposals", label: "My proposals" },
  { href: "/freelancer/offers", label: "Offers" },
  { href: "/freelancer/contracts", label: "Contracts" },
  { href: "/freelancer/profile", label: "Profile" },
];

export default function FreelancerLayout() {
  return <InfrastructureShell eyebrow="Independent work" title="Freelancer workspace" navigation={navigation}><Outlet /></InfrastructureShell>;
}