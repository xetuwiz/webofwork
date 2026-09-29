import { Component } from 'react';
import { AlertTriangle, RefreshCw, Home, WifiOff } from 'lucide-react';

/**
 * Error boundary with a themed fallback.
 *
 * The app previously had one boundary, mounted once around the whole SPA. That
 * catches a crash, but it means any error anywhere -- a settings page, a single
 * card, a route that failed to lazy-load -- replaces the entire UI with a full
 * screen takeover. Wrapping route groups and individual panels in this gives a
 * local fallback and leaves the shell (nav, header, theme) usable.
 *
 * Two things it specifically handles:
 *
 * 1. **Lazy chunk failures.** A `React.lazy` route that fails to fetch its
 *    chunk throws during render. That is the single most common new failure
 *    once routes are code-split, and the default behaviour ("Reload") is
 *    usually right because a deploy invalidated the old chunk hashes.
 *
 * 2. **A real message.** The old fallback said "System Interrupt" and "Security
 *    Protocol: Active // WebOfWork_Shield_v1.0" while showing a raw exception
 *    string. Users are not operators; the copy now says what happened and what
 *    to do, and the technical detail goes to the console.
 */
export class ErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = { error: null };
  }

  static getDerivedStateFromError(error) {
    return { error };
  }

  componentDidCatch(error, errorInfo) {
    // Keep the technical detail in the console, where it is useful, instead of
    // printing an exception message to the user.
    console.error('[ErrorBoundary]', error, errorInfo);
  }

  handleReload = () => {
    window.location.reload();
  };

  handleReset = () => {
    this.setState({ error: null });
  };

  render() {
    const { error } = this.state;
    const { children, label = 'this section' } = this.props;

    if (!error) return children;

    // A failed dynamic import gives a TypeError mentioning "import" or a fetch
    // failure, and almost always means a stale chunk after a deploy.
    const message = /dynamically imported module|Importing a module script failed|Failed to fetch|Loading chunk/i.test(
      error.message || ''
    )
      ? 'A newer version of the site is available. Reload to get the latest.'
      : 'Something went wrong loading this part of the page.';

    return (
      <div
        role="alert"
        className="rounded-xl border border-line bg-surface-2 p-6 text-center"
      >
        <div className="mx-auto mb-3 flex h-10 w-10 items-center justify-center rounded-full bg-danger-soft text-danger-soft-fg">
          <AlertTriangle className="h-5 w-5" aria-hidden="true" />
        </div>
        <h2 className="text-sm font-bold text-fg">{label} could not be displayed</h2>
        <p className="mx-auto mt-1.5 max-w-sm text-xs leading-relaxed text-fg-muted">
          {message} The rest of the page is still usable.
        </p>
        <div className="mt-4 flex items-center justify-center gap-2">
          <button
            type="button"
            onClick={this.handleReset}
            className="rounded-lg border border-line px-3 py-1.5 text-xs font-semibold text-fg-muted transition-colors hover:bg-surface-3 hover:text-fg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand"
          >
            Try again
          </button>
          <button
            type="button"
            onClick={this.handleReload}
            className="rounded-lg bg-brand px-3 py-1.5 text-xs font-semibold text-brand-fg transition-colors hover:bg-brand-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand"
          >
            Reload page
          </button>
        </div>
      </div>
    );
  }
}

/**
 * Full-screen fallback for the app shell. Used once, at the root, where there is
 * no surrounding UI left to preserve.
 */
export class RootErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = { error: null, offline: !navigator.onLine };
  }

  static getDerivedStateFromError(error) {
    return { error };
  }

  componentDidCatch(error, errorInfo) {
    console.error('[RootErrorBoundary]', error, errorInfo);
  }

  componentDidMount() {
    window.addEventListener('online', this.handleOnline);
    window.addEventListener('offline', this.handleOffline);
  }

  componentWillUnmount() {
    window.removeEventListener('online', this.handleOnline);
    window.removeEventListener('offline', this.handleOffline);
  }

  handleOnline = () => this.setState({ offline: false });
  handleOffline = () => this.setState({ offline: true });

  render() {
    const { error, offline } = this.state;

    // Offline is not an error, but it looks like one: every request fails and
    // the user is told something broke. Worth calling out separately.
    if (offline && !error) return null;

    if (!error) return this.props.children;

    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-surface-2 px-6 py-16 text-center">
        <div className="w-full max-w-md rounded-2xl border border-line bg-surface p-8 shadow-sm">
          <div className="mx-auto mb-5 flex h-14 w-14 items-center justify-center rounded-2xl bg-danger-soft text-danger-soft-fg">
            {offline ? (
              <WifiOff className="h-7 w-7" aria-hidden="true" />
            ) : (
              <AlertTriangle className="h-7 w-7" aria-hidden="true" />
            )}
          </div>

          <h1 className="text-xl font-bold tracking-tight text-fg">
            {offline ? "You're offline" : 'This page hit a problem'}
          </h1>
          <p className="mt-2 text-sm leading-relaxed text-fg-muted">
            {offline
              ? 'Check your internet connection and try again. Nothing you were working on has been lost.'
              : 'An unexpected error stopped the page from loading. Reloading usually fixes it.'}
          </p>

          <div className="mt-6 flex flex-col gap-2">
            <button
              type="button"
              onClick={() => window.location.reload()}
              className="flex w-full items-center justify-center gap-2 rounded-xl bg-brand px-4 py-3 text-sm font-semibold text-brand-fg transition-colors hover:bg-brand-hover focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand"
            >
              <RefreshCw className="h-4 w-4" aria-hidden="true" />
              Reload page
            </button>
            <a
              href="/"
              className="flex w-full items-center justify-center gap-2 rounded-xl border border-line px-4 py-3 text-sm font-semibold text-fg-muted transition-colors hover:bg-surface-2 hover:text-fg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-brand"
            >
              <Home className="h-4 w-4" aria-hidden="true" />
              Go to homepage
            </a>
          </div>
        </div>
      </div>
    );
  }
}

export default ErrorBoundary;
