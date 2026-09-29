import { useSelector } from "react-redux";
import { Link } from "react-router-dom";
import { SOCIAL_LINKS } from "../../../../config/socialLinks";
import Footer from "../../landingPage/components/Footer";
import FreelancerFooter from "../../../freelancer/dashboard/components/FreelancerFooter";
import AdminFooter from "../../../admin/dashboard/components/AdminFooter";

export default function ClientFooter() {
  const year = new Date().getFullYear();
  const user = useSelector((state) => state.auth.user);

  if (!user) {
    return <Footer />;
  }

  if (user?.current_role === "freelancer") {
    return <FreelancerFooter />;
  }
  
  if (user?.is_admin) {
    return <AdminFooter />;
  }

  return (
    <footer className="border-t border-line bg-surface">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 py-12">
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-10 lg:gap-8">
          
          <div className="flex flex-col items-center sm:items-start gap-3">
            <h2 className="text-3xl md:text-[37px] font-bold leading-tight" style={{ fontFamily: 'MuseoModerno, sans-serif' }}>
              <span className="text-fg">Web</span>
              <span className="text-brand">OfWork</span>
            </h2>
            <p className="text-sm font-medium text-fg-muted sm:ml-2" style={{ fontFamily: 'Murecho, sans-serif' }}>
              Where Great Work Meets Great Talent.
            </p>
          </div>

          <div className="flex flex-col items-center sm:items-start gap-4">
            <h3 className="text-base font-bold text-fg" style={{ fontFamily: 'Space Grotesk, sans-serif' }}>
              Company
            </h3>
            <nav className="flex flex-col items-center sm:items-start gap-2">
              <Link to='/about-us' className="text-sm font-bold text-fg-subtle hover:text-brand transition-colors" style={{ fontFamily: 'Space Grotesk, sans-serif' }}>
                About Us
              </Link>
              <Link to='/contact-us' className="text-sm font-bold text-fg-subtle hover:text-brand transition-colors" style={{ fontFamily: 'Space Grotesk, sans-serif' }}>
                Contact
              </Link>
            </nav>
          </div>

          <div className="flex flex-col items-center sm:items-start gap-4">
            <h3 className="text-base font-bold text-fg" style={{ fontFamily: 'Space Grotesk, sans-serif' }}>
              Support
            </h3>
            <nav className="flex flex-col items-center sm:items-start gap-2">
              <Link to='/faq' className="text-sm font-bold text-fg-subtle hover:text-brand transition-colors" style={{ fontFamily: 'Space Grotesk, sans-serif' }}>
                FAQs
              </Link>
              <Link to='/support' className="text-sm font-bold text-fg-subtle hover:text-brand transition-colors" style={{ fontFamily: 'Space Grotesk, sans-serif' }}>
                Support
              </Link>
            </nav>
          </div>

          <div className="flex flex-col items-center sm:items-start gap-4">
            <h3 className="text-base font-bold text-fg" style={{ fontFamily: 'Space Grotesk, sans-serif' }}>
              Legal
            </h3>
            <nav className="flex flex-col items-center sm:items-start gap-2">
              <Link to='/terms-and-conditions' className="text-sm font-bold text-fg-subtle hover:text-brand transition-colors" style={{ fontFamily: 'Space Grotesk, sans-serif' }}>
                Terms & Conditions
              </Link>
              <Link to='/privacy-policies' className="text-sm font-bold text-fg-subtle hover:text-brand transition-colors" style={{ fontFamily: 'Space Grotesk, sans-serif' }}>
                Privacy Policy
              </Link>
            </nav>
          </div>
        </div>

        <div className="mt-12 flex flex-col items-center justify-between gap-6 border-t border-line pt-8 md:flex-row">
          <p className="text-sm font-bold text-fg-subtle text-center md:text-left" style={{ fontFamily: 'Space Grotesk, sans-serif' }}>
            © {year} <Link to='/'><span className="text-fg hover:text-brand transition-colors">Web</span><span className="text-brand">OfWork.</span></Link> All rights reserved.
          </p>
          
          {/*
            Social links used to sit here as two "Follow us" buttons, labelled
            as though they belonged to the company, but both hrefs pointed at one
            individual's personal X and LinkedIn accounts. That is a real problem
            beyond the stray name: the product shipped a link to somebody's
            personal profile and presented it as its own.

            They are removed rather than repointed, because inventing a plausible
            WebOfWork handle would just replace a wrong link with a dead one.
            When real accounts exist, add them to SOCIAL_LINKS in
            `src/config/socialLinks.js` and this block renders them -- no JSX
            edit needed, and no way to end up back on a personal profile.
          */}
          {SOCIAL_LINKS.length > 0 && (
            <div className="flex items-center gap-6">
              {/*
                The icon is pulled into a `const` rather than destructured
                straight into the map callback's parameter list. This project's
                eslint config has no `react/jsx-uses-vars`; it leans on
                `no-unused-vars` with `varsIgnorePattern: '^[A-Z_]'` to excuse
                components referenced only in JSX. That pattern only applies to
                *variables* -- a destructured function parameter is checked
                against `argsIgnorePattern`, which is not set, so
                `({ icon: Icon })` was reported as an unused variable even though
                `<Icon />` sits three lines below it. A `const` is covered by the
                existing rule, so this needs no config change.
              */}
              {SOCIAL_LINKS.map((social) => {
                const IconComponent = social.icon;
                return (
                  <a
                    key={social.label}
                    href={social.href}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="text-fg-subtle hover:text-brand transition-colors text-xl"
                    aria-label={`Follow us on ${social.label}`}
                  >
                    <IconComponent aria-hidden="true" />
                  </a>
                );
              })}
            </div>
          )}
        </div>
      </div>
    </footer>
  );
}