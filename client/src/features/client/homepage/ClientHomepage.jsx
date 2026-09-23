import ClientHeroSection from "./components/ClientHeroSection.jsx";
import ClientWhyUs from "./components/ClientWhyUs.jsx";
import TopFreelancers from "./components/TopFreelancers.jsx";
import JoinNow from "./components/JoinNow.jsx";
import ClientHeader from "./components/ClientHeader.jsx";
import ClientFooter from "./components/ClientFooter.jsx";

export default function ClientHomepage() {
  return <><ClientHeader /><main className="public-page"><ClientHeroSection /><ClientWhyUs /><TopFreelancers /><JoinNow /></main><ClientFooter /></>;
}