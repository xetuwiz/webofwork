import { useState } from "react";
import { Link, useLocation } from "react-router-dom";
import { useSelector } from "react-redux";
import ProfileModal from "./ProfileModal.jsx";

export default function ClientHeader() {
  const user = useSelector((state) => state.auth.user);
  const location = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);
  const [profileOpen, setProfileOpen] = useState(false);
  const firstName = (user?.fullName ?? user?.FullName ?? "Client").split(" ")[0];
  const links = [["Dashboard", "/home"], ["Find talent", "/find-talents"], ["How it works", "/how-it-works"], ["My projects", "/my-projects"]];
  const isActive = (href) => location.pathname === href || location.pathname.startsWith(`${href}/`);

  return (
    <header className="client-home-header">
      <Link className="public-header__brand" to="/" aria-label="WebOfWork home">
        <span className="public-header__mark">W</span>
        <span>WebOfWork</span>
      </Link>
      <button type="button" className="client-home-header__menu" aria-expanded={menuOpen} onClick={() => setMenuOpen((open) => !open)}>
        {menuOpen ? "Close" : "Menu"}
      </button>
      <nav className={menuOpen ? "client-home-header__nav client-home-header__nav--open" : "client-home-header__nav"} aria-label="Client navigation">
        {links.map(([label, href]) => <Link className={isActive(href) ? "client-home-header__link client-home-header__link--active" : "client-home-header__link"} key={href} to={href} onClick={() => setMenuOpen(false)}>{label}</Link>)}
        <Link className="button button--accent" to="/create-project" onClick={() => setMenuOpen(false)}>Post a project</Link>
        <button type="button" className="client-home-header__profile" aria-expanded={profileOpen} onClick={() => setProfileOpen((open) => !open)}>
          <span>{firstName}</span>
          <strong>{firstName.slice(0, 1).toUpperCase()}</strong>
        </button>
      </nav>
      {profileOpen && <ProfileModal onClose={() => setProfileOpen(false)} />}
    </header>
  );
}