import { ExternalLink, X } from "lucide-react";
import { useModal } from "../../../../hooks/modal/useModalStore";

/**
 * Split-banner shell shared by the sign-in and sign-up modals.
 *
 * ## The scrim here was a bug, and it was invisible to the audit
 *
 * This rendered its own `fixed inset-0 z-modal` backdrop *inside*
 * `ModelWrapper`, which is itself a `<Dialog>`. So sign-in and sign-up had two
 * stacked scrims: Radix's from the wrapper, and this second one painted over
 * the panel content.
 *
 * That second scrim is why the modal count was wrong twice. A scan for
 * `fixed inset-0 ... z-modal` lists this file and it looks exactly like a
 * hand-rolled modal, but it is not one -- it is a stray backdrop inside a
 * component that already has a Dialog around it. "Converting" it to `<Dialog>`
 * would have produced a dialog inside a dialog.
 *
 * The fix is deletion, not migration: the wrapper owns the scrim, focus trap,
 * Escape handling and scroll lock. All this needs to contribute is layout.
 */
export default function AuthLayout({ title, children, onClose }) {
  const { openModal } = useModal();

  return (
    <>
      {/* Container: Full screen on mobile, capped on desktop.

          The `md:max-w-page-lg` (1024px) that used to be here fought the Dialog
          panel and is gone. `ModelWrapper` now opens these at `size="xl"`
          (1100px), so the old cap left ~38px of empty panel on each side -- the
          form got no wider while the dialog visibly did. Letting the child fill
          its panel is also the correct division of labour: the panel owns the
          width, this owns the layout inside it. */}
      <div className="w-full h-full md:h-auto md:max-h-[90vh] lg:h-[780px] flex bg-surface md:rounded-[28px] overflow-hidden shadow-2xl relative transition-all duration-slow">
        
        {/* Close Button */}
        {onClose && (
          <button
            onClick={onClose}
            className="absolute top-4 right-4 z-raised p-2 bg-surface/80 backdrop-blur-md border border-line-soft rounded-full text-fg-muted hover:text-fg hover:bg-surface transition-all active:scale-95 shadow-sm"
          >
            <X className="w-5 h-5 md:w-6 md:h-6" />
          </button>
        )}

        {/* Banner Image: Hidden on mobile, 40% on small desktop, 60% on large desktop */}
        <div className="hidden md:block md:w-[45%] lg:w-[60%] shrink-0 relative bg-surface-3">
          <img
            src="https://api.builder.io/api/v1/image/assets/TEMP/d5a056a1a9dcfe6edb4f50fccc858ae5f35d3183?width=1292"
            alt="Auth Banner"
            className="w-full h-full object-cover"
          />
          {/* Subtle overlay for better contrast */}
          <div className="absolute inset-0 bg-black/5" />
        </div>

        {/* Form Section: Full width on mobile, 55% to 40% on desktop */}
        <div className="w-full md:w-[55%] lg:w-[40%] bg-surface flex flex-col h-full">
          <div className="flex-1 flex flex-col items-center justify-center p-6 sm:p-10 md:p-8 lg:p-12 overflow-y-auto">
            
            <div className="w-full max-w-sm lg:max-w-none">
              <div className="text-center mb-6 lg:mb-8">
                <h1 className="font-Museo font-black text-[28px] md:text-[32px] lg:text-[40px] text-fg tracking-tight leading-tight">
                  {title}
                </h1>
                
                {title === 'Welcome Back' && (
                  <div className="flex flex-wrap items-center justify-center gap-1.5 text-xs sm:text-sm text-fg-subtle mt-2 font-medium">
                    <span>Don't have an account?</span>
                    <button 
                      onClick={() => openModal('signup')} 
                      className="text-brand font-bold hover:text-brand hover:underline flex items-center gap-1 transition-colors"
                    >
                      <span>Sign up</span>
                      <ExternalLink size={14} aria-hidden="true" />
                    </button>
                  </div>
                )}
              </div>

              <div className="w-full">
                {children}
              </div>
            </div>

          </div>
        </div>
      </div>
    </>
  );
}
