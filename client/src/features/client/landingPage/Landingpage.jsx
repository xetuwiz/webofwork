import { useEffect } from "react";
import { useSelector } from "react-redux";
import { useNavigate } from "react-router-dom";
import Header from "./components/Header.jsx";
import HeroSection from "./components/HeroSection.jsx";
import StepsSection from "./components/StepsSection.jsx";
import CategoriesSection from "./components/CategoriesSection.jsx";
import Features from "./components/Features.jsx";
import Testimonials from "./components/Testimonials.jsx";
import CTASection from "./components/CTASection.jsx";
import Footer from "./components/Footer.jsx";

export default function Landingpage() {
  const user = useSelector((state) => state.auth.user);
  const navigate = useNavigate();

  useEffect(() => {
    if (!user) return;

    const role = String(user.role ?? user.Role ?? "").toLowerCase();
    const destination = role === "admin" || role === "3"
      ? "/admin"
      : role === "freelancer" || role === "2"
        ? "/freelancer"
        : "/home";

    navigate(destination, { replace: true });
  }, [navigate, user]);

  if (user) return null;
  return <><Header /><main className="public-page"><HeroSection /><StepsSection /><CategoriesSection /><Features /><Testimonials /><CTASection /></main><Footer /></>;
}