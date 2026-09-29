import { Link } from "react-router-dom";

export default function FreelancerFooter() {
  const d = new Date();
let year = d.getFullYear();
  return (
    <footer className="w-full bg-surface border-t border-line-soft">
      <div className="max-w-page-xl mx-auto px-6 h-30 flex flex-col items-center justify-center">
        <nav className="flex items-center justify-center gap-6 mb-8">
          <Link 
            to="/freelancer/support" 
            className="text-brand text-xs font-normal hover:underline"
          >
            Support
          </Link>
          <span className="text-fg-subtle text-sm">|</span>
          <Link 
            to="/freelancer/faq" 
            className="text-brand text-xs font-normal hover:underline"
          >
            FAQ
          </Link>
          <span className="text-fg-subtle text-sm">|</span>
          <Link 
            to="/freelancer/terms-and-conditions" 
            className="text-brand text-xs font-normal hover:underline"
          >
            Terms
          </Link>
          <span className="text-fg-subtle text-sm">|</span>
          <Link 
            to="/freelancer/privacy-policies" 
            className="text-brand text-xs font-normal hover:underline"
          >
            Privacy
          </Link>
        </nav>
        <p className="text-fg-muted text-sm text-center">
          © {year} <Link to={'/'}><span className="text-fg">Web</span><span className="text-brand">OfWork.</span></Link> Where Great Work Meets Great Talent.
        </p>
      </div>
    </footer>
  );
}
