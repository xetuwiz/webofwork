import { useState } from "react";
import { Link } from "react-router-dom";
import { useModal } from "../../../../hooks/modal/useModalStore.jsx";

export default function Header() {
  const { openModal } = useModal();
  const [menuOpen, setMenuOpen] = useState(false);
  const links = [["About us", "/about-us"], ["Why WebOfWork", "/why-us"], ["How it works", "/how-it-works"]];

  const closeMenu = () => setMenuOpen(false);

  return (
    <header className="public-header">
      <Link className="public-header__brand" to="/" onClick={closeMenu}>
        <span className="public-header__mark">W</span>
        <span>WebOfWork</span>
      </Link>
      <nav className={menuOpen ? "public-header__nav public-header__nav--open" : "public-header__nav"}>
        {links.map(([label, href]) => <Link key={href} to={href} onClick={closeMenu}>{label}</Link>)}
        <button type="button" className="button button--quiet" onClick={() => { openModal("signin"); closeMenu(); }}>Log in</button>
        <button type="button" className="button button--accent" onClick={() => { openModal("signup"); closeMenu(); }}>Join WebOfWork</button>
      </nav>
      <button type="button" className="public-header__menu" aria-expanded={menuOpen} aria-label="Toggle navigation" onClick={() => setMenuOpen((open) => !open)}>
        {menuOpen ? "Close" : "Menu"}
      </button>
    </header>
  );
}