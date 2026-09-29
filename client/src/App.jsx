import { lazy, Suspense } from 'react';
import { RootErrorBoundary } from './components/common/ErrorBoundary';
import AppToaster from './components/common/AppToaster';
import OfflineBanner from './components/common/OfflineBanner';

// The router is the one module that must load before anything can render, so it
// is the right thing to code-split: everything it pulls in is code-split with it.
const AppRoutes = lazy(() => import('./routes/AppRoutes'));

export default function App() {
  return (
    <RootErrorBoundary>
      <OfflineBanner />
      {/*
        AppRoutes is lazy, so its chunk can fail to load on its own -- a deploy
        that invalidates the previous chunk hashes is the ordinary case. Without
        a boundary here that failure takes down the whole app, including the
        offline banner above it.
      */}
      <Suspense fallback={<AppFallback />}>
        <AppRoutes />
      </Suspense>
      <AppToaster />
    </RootErrorBoundary>
  );
}

/**
 * First-paint fallback while the router chunk loads. A full-height spinner
 * rather than a skeleton: the shell's shape differs per role, so a
 * content-shaped placeholder would be wrong most of the time and would jump.
 */
function AppFallback() {
  return (
    <div
      className="flex min-h-screen items-center justify-center bg-surface-2"
      role="status"
      aria-live="polite"
    >
      <span className="sr-only">Loading</span>
      <div
        aria-hidden="true"
        className="h-8 w-8 animate-spin rounded-full border-[3px] border-line border-t-brand"
      />
    </div>
  );
}
