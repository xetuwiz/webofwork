import { Link, useLocation } from "react-router-dom";

function isActivePath(pathname, href) {
  if (href === "/") return pathname === "/";
  return pathname === href || pathname.startsWith(`${href}/`);
}

export default function InfrastructureShell({ eyebrow, title, navigation = [], children, footer = "WebOfWork" }) {
  const location = useLocation();

  return (
    <div className="app-shell">
      <header className="app-shell__header">
        <Link className="app-shell__brand" to="/">
          <span className="app-shell__brand-mark" aria-hidden="true">W</span>
          <span>WebOfWork</span>
        </Link>
        <div className="app-shell__header-copy">
          <span className="app-shell__eyebrow">{eyebrow}</span>
          <strong>{title}</strong>
        </div>
      </header>
      <div className="app-shell__body">
        <aside className="app-shell__sidebar" aria-label={`${title} navigation`}>
          <nav className="app-shell__nav">
            {navigation.map((item) => (
              <Link className={isActivePath(location.pathname, item.href) ? "app-shell__link app-shell__link--active" : "app-shell__link"} key={item.href} to={item.href}>
                <span>{item.label}</span>
                {item.meta && <small>{item.meta}</small>}
              </Link>
            ))}
          </nav>
        </aside>
        <main className="app-shell__main">{children}</main>
      </div>
      <footer className="app-shell__footer">{footer}</footer>
    </div>
  );
}