import { useModal } from "../../../../hooks/modal/useModalStore";
import {
  X,
  Wallet,
  Calendar,
  Tag,
  Briefcase,
  CheckCircle2,
  AlertCircle
} from 'lucide-react';
import Dialog from "../../../../components/common/Dialog";

function ConfirmProjectModal({ isPending, onConfirm }) {
  const { modalProps, closeModal } = useModal();
  const { data, categories } = modalProps;

  if (!data) return null;

  const categoryName =
    categories?.find((c) => String(c.id) === String(data.category))?.name ??
    "—";

  return (
    <Dialog
            open
            onClose={closeModal}
      title="Confirm Project"
      size="lg"
      className="overflow-hidden"
    >

        <div className="flex items-center justify-between px-6 py-4 border-b border-line-soft">
          <div className="flex items-center gap-2">
            <div className="bg-brand-soft p-2 rounded-lg">
              <CheckCircle2 className="w-5 h-5 text-brand" />
            </div>
            <h2 className="text-xl font-bold text-fg font-inter">
              Review Project
            </h2>
          </div>
          <button
            onClick={closeModal}
            className="text-fg-subtle hover:text-fg-muted transition-colors"
          >
            <X className="w-6 h-6" />
          </button>
        </div>

        <div className="p-6 max-h-[70vh] overflow-y-auto">
          <div className="space-y-6">

            <div>
              {/* Same misuse as "Required Skills" below: this <label> heads an
                  <h3>, not a control. */}
              <p className="text-[11px] uppercase tracking-wider font-bold text-fg-subtle mb-1">Project Title</p>
              <h3 className="text-lg font-semibold text-fg leading-snug">
                {data.title}
              </h3>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="bg-surface-2 p-4 rounded-xl border border-line-soft">
                <div className="flex items-center gap-2 text-fg-subtle mb-1">
                  <Tag className="w-4 h-4" />
                  <span className="text-xs font-medium uppercase tracking-tight">Category</span>
                </div>
                <p className="font-semibold text-fg-muted">{categoryName}</p>
              </div>

              <div className="bg-surface-2 p-4 rounded-xl border border-line-soft">
                <div className="flex items-center gap-2 text-fg-subtle mb-1">
                  <Wallet className="w-4 h-4" />
                  <span className="text-xs font-medium uppercase tracking-tight">
                    Budget
                  </span>
                </div>

                {data.pricing_type === "fixed" ? (
                  <p className="font-semibold text-fg-muted">
                    ${data.fixed_price.toLocaleString()}
                  </p>
                ) : (
                  <p className="font-semibold text-fg-muted">
                    ${data.min_budget.toLocaleString()} – ${data.max_budget.toLocaleString()}
                  </p>
                )}
              </div>


              <div className="bg-surface-2 p-4 rounded-xl border border-line-soft">
                <div className="flex items-center gap-2 text-fg-subtle mb-1">
                  <Calendar className="w-4 h-4" />
                  <span className="text-xs font-medium uppercase tracking-tight">Timeline</span>
                </div>
                <p className="font-semibold text-fg-muted">{data.delivery_days} Days</p>
              </div>

              <div className="bg-surface-2 p-4 rounded-xl border border-line-soft">
                <div className="flex items-center gap-2 text-fg-subtle mb-1">
                  <Briefcase className="w-4 h-4" />
                  <span className="text-xs font-medium uppercase tracking-tight">Type</span>
                </div>
                <p className="font-semibold text-fg-muted">
                  {data.pricing_type === "fixed" ? "Fixed Price" : "Range Price"}
                </p>

              </div>
            </div>

            <div>
              {/* Was a <label>, which is a third defect class: a label names a
                  form control, and this names a list of chips. There is no
                  control to associate with, so no amount of htmlFor would make
                  it correct. It is a heading, and now says so. */}
              <p className="text-[11px] uppercase tracking-wider font-bold text-fg-subtle mb-2">Required Skills</p>
              <div className="flex flex-wrap gap-2">
                {data.skills.map((skill, idx) => (
                  <span
                    key={idx}
                    className="px-3 py-1 bg-brand-soft text-brand text-xs font-semibold rounded-full border border-brand-line"
                  >
                    {skill}
                  </span>
                ))}
              </div>
            </div>

            <div className="flex gap-3 p-4 bg-warning-soft rounded-lg border border-warning/30">
              <AlertCircle className="w-5 h-5 text-warning-soft-fg shrink-0" />
              <p className="text-xs text-warning leading-relaxed">
                Please double-check your budget and timeline. Once posted, the project will be visible to all qualified freelancers.
              </p>
            </div>
          </div>
        </div>

        <div className="bg-surface-2 px-6 py-4 flex items-center justify-end gap-3 border-t border-line-soft">
          <button
            onClick={closeModal}
            className="px-5 py-2.5 text-sm font-semibold text-fg-muted hover:text-fg transition-all hover:bg-surface-inset rounded-xl"
          >
            Go Back & Edit
          </button>

          <button
            onClick={onConfirm}
            disabled={isPending}
            className="px-8 py-2.5 bg-brand hover:bg-brand-hover text-brand-fg rounded-xl text-sm font-bold shadow-lg shadow-blue-200 transition-all active:scale-95 disabled:opacity-60 flex items-center gap-2"
          >
            {isPending ? (
              <>
                <div className="w-4 h-4 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                Processing...
              </>
            ) : (
              "Confirm & Post Project"
            )}
          </button>
        </div>
    </Dialog>
  );
}

export default ConfirmProjectModal;
