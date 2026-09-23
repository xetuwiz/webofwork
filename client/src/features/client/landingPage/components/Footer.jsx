import { Link } from "react-router-dom";

export default function Footer() {
  return (
    <footer className="public-footer">
      <div>
        <strong>WebOfWork</strong>
        <p>Structured work for serious clients and independent talent.</p>
      </div>
      <nav className="public-footer__links" aria-label="Footer navigation">
        <Link to="/about-us">About</Link>
        <Link to="/faq">FAQ</Link>
        <Link to="/privacy-policies">Privacy</Link>
        <Link to="/terms-and-conditions">Terms</Link>
        <Link to="/contact-us">Contact</Link>
      </nav>
      <small>© {new Date().getFullYear()} WebOfWork</small>
    </footer>
  );
}