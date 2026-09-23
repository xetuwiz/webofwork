import { Link } from "react-router-dom";

export default function ClientFooter() {
  return (
    <footer className="client-home-footer">
      <div><strong>WebOfWork</strong><p>Structured work for serious clients and independent talent.</p></div>
      <nav aria-label="Client footer navigation">
        <Link to="/about-us">About</Link>
        <Link to="/faq">FAQ</Link>
        <Link to="/privacy-policies">Privacy</Link>
        <Link to="/terms-and-conditions">Terms</Link>
        <Link to="/support">Support</Link>
      </nav>
      <small>© {new Date().getFullYear()} WebOfWork</small>
    </footer>
  );
}