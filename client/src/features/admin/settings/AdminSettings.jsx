import { useState, useEffect } from "react";
import { FileText, Shield, HelpCircle, Loader2, CreditCard } from "lucide-react";
import { useForm } from "react-hook-form";
import { useGlobalSettings } from "./adminSettingsQueries";
import { useUpdateSettings } from "./adminsettingsMutations";
import { Link } from "react-router-dom";
import Dialog from "../../../components/common/Dialog";
import { Field } from "../../../components/common/Field";

export default function AdminSettings() {
  const { data: settings, isLoading } = useGlobalSettings();
  const updateMutation = useUpdateSettings();
  const [activeModal, setActiveModal] = useState(null);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm();

  useEffect(() => {
    if (settings) {
      reset({
        commission_percentage: settings.commission_percentage,
        support_email: settings.support_email,
        paypal_email: settings.paypal_email,
      });
    }
  }, [settings, reset]);

  const onSubmit = (data) => {
    updateMutation.mutate(data, {
      onSuccess: () => setActiveModal(null),
    });
  };

  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-brand" />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-surface-2 px-4 py-8 md:px-8">
      <div className="mx-auto max-w-page-lg">
        <div className="mb-8 space-y-2">
          <h1 className="text-[26px] font-bold leading-9 text-fg">Platform Settings</h1>
          <p className="text-sm text-fg-muted">Configure your platform's core settings, integrations, and payouts.</p>
        </div>

        <div className="space-y-8">
          <div className="rounded-lg border border-line bg-surface p-6 shadow-sm">
            <h2 className="mb-6 text-[15px] font-semibold text-fg">Platform Commission Percentage</h2>
            <div className="flex items-center justify-between">
              <div className="space-y-2">
                <div className="text-[31px] font-bold leading-10 text-brand">
                  {settings?.commission_percentage} %
                </div>
                <p className="text-xs text-fg-muted">This percentage is deducted from every successful order.</p>
              </div>
              <button 
                onClick={() => setActiveModal('commission')}
                className="rounded-md border border-line-strong bg-transparent px-[17px] py-[11px] text-xs font-medium text-fg-muted transition-colors hover:bg-surface-2"
              >
                Edit
              </button>
            </div>
          </div>

          <div className="rounded-lg border border-line bg-surface p-6 shadow-sm">
            <h2 className="mb-6 text-[15px] font-semibold text-fg">Support Email Address</h2>
            <div className="flex items-center justify-between">
              <div className="space-y-2">
                <div className="text-base font-normal text-fg">{settings?.support_email}</div>
                <p className="text-xs text-fg-muted">Primary contact email for customer support inquiries.</p>
              </div>
              <button 
                onClick={() => setActiveModal('email')}
                className="rounded-md border border-line-strong bg-transparent px-[17px] py-[11px] text-xs font-medium text-fg-muted transition-colors hover:bg-surface-2"
              >
                Update Email
              </button>
            </div>
          </div>

          <div className="rounded-lg border border-line bg-surface p-6 shadow-sm">
            <div className="flex items-center gap-2 mb-6">
              <CreditCard className="h-4 w-4 text-fg-subtle" />
              <h2 className="text-[15px] font-semibold text-fg">Master PayPal Account</h2>
            </div>
            <div className="flex items-center justify-between">
              <div className="space-y-2">
                <div className="text-base font-normal text-fg">{settings?.paypal_email}</div>
                <p className="text-xs text-fg-muted">The business PayPal email used for processing platform payouts.</p>
              </div>
              <button 
                onClick={() => setActiveModal('paypal')}
                className="rounded-md border border-line-strong bg-transparent px-[17px] py-[11px] text-xs font-medium text-fg-muted transition-colors hover:bg-surface-2"
              >
                Update PayPal
              </button>
            </div>
          </div>

          <div className="rounded-lg border border-line bg-surface p-6 shadow-sm opacity-80">
            <h2 className="mb-2 text-[15px] font-semibold text-fg">Manage Site Pages</h2>
            <p className="mb-6 text-xs text-fg-muted">Edit Terms & Conditions, Privacy Policy, and FAQ.</p>
            <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
              {[
                { title: "Terms & Conditions", icon: FileText, desc: "Manage legal agreements.", link:'/admin/terms-and-conditions' },
                { title: "Privacy Policy", icon: Shield, desc: "Edit data protection info.", link:'/admin/privacy-policy/' },
                { title: "FAQ", icon: HelpCircle, desc: "Manage frequently asked questions.", link:'/admin/faq-management/' }
              ].map((item, idx) => (
                <div key={idx} className="flex gap-4">
                  <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-lg bg-brand-soft">
                    <item.icon className="h-6 w-6 text-brand" />
                  </div>
                  <div className="flex-1 space-y-4">
                    <h3 className="text-[15px] font-semibold text-fg">{item.title}</h3>
                    <Link to={item.link} className="rounded-md border border-line-strong px-3 py-2 text-xs font-medium text-fg-muted hover:bg-surface-2">Manage {item.title}</Link>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>

      {/* Was a hand-rolled `fixed inset-0 z-50` scrim, so it had no focus trap,
          no Esc to close, no scroll lock, and no `aria-modal` -- the page behind
          stayed reachable by keyboard. <Dialog> supplies all of those. The three
          inputs also had labels with no `htmlFor` and no matching id, so they
          were announced as bare "edit text". */}
      <Dialog
        open={!!activeModal}
        onClose={() => setActiveModal(null)}
        title={
          activeModal === "commission"
            ? "Edit Commission"
            : activeModal === "paypal"
              ? "Update PayPal Email"
              : "Update Support Email"
        }
        size="sm"
      >
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
          {activeModal === "commission" && (
            <Field label="Percentage (%)" error={errors.commission_percentage?.message}>
              {(f) => (
                <div className="relative">
                  <input
                    type="number"
                    step="0.01"
                    {...register("commission_percentage", {
                      required: "Commission is required",
                      min: { value: 0, message: "Minimum 0%" },
                      max: { value: 100, message: "Maximum 100%" },
                    })}
                    {...f}
                    className={`w-full rounded-lg border px-4 py-3 pr-10 text-sm outline-none focus-visible:ring-2 ${errors.commission_percentage ? "border-danger focus-visible:ring-danger/20" : "border-line focus-visible:ring-brand/20"}`}
                  />
                  <span
                    className="absolute right-4 top-1/2 -translate-y-1/2 text-fg-subtle"
                    aria-hidden="true"
                  >
                    %
                  </span>
                </div>
              )}
            </Field>
          )}

          {activeModal === "email" && (
            <Field label="New Support Email" error={errors.support_email?.message}>
              {(f) => (
                <input
                  type="email"
                  {...register("support_email", {
                    required: "Email is required",
                    pattern: {
                      value: /^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$/i,
                      message: "Invalid email address",
                    },
                  })}
                  {...f}
                  className={`w-full rounded-lg border px-4 py-3 text-sm outline-none focus-visible:ring-2 ${errors.support_email ? "border-danger focus-visible:ring-danger/20" : "border-line focus-visible:ring-brand/20"}`}
                  placeholder="support@example.com"
                />
              )}
            </Field>
          )}

          {activeModal === "paypal" && (
            <Field label="Business PayPal Email" error={errors.paypal_email?.message}>
              {(f) => (
                <input
                  type="email"
                  {...register("paypal_email", {
                    required: "PayPal email is required",
                    pattern: {
                      value: /^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$/i,
                      message: "Invalid email address",
                    },
                  })}
                  {...f}
                  className={`w-full rounded-lg border px-4 py-3 text-sm outline-none focus-visible:ring-2 ${errors.paypal_email ? "border-danger focus-visible:ring-danger/20" : "border-line focus-visible:ring-brand/20"}`}
                  placeholder="business@paypal.com"
                />
              )}
            </Field>
          )}

          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={() => setActiveModal(null)}
              className="flex-1 rounded-lg border border-line-strong py-3 text-sm font-semibold text-fg-muted hover:bg-surface-2 transition-colors"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={updateMutation.isPending}
              className="flex-1 rounded-lg bg-brand py-3 text-sm font-semibold text-brand-fg shadow-md hover:bg-brand-hover transition-all disabled:opacity-50 active:scale-[0.98]"
            >
              {updateMutation.isPending ? "Saving..." : "Save Changes"}
            </button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}