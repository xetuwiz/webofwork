import { Mail, Lock } from "lucide-react";
import PasswordInput from '../../../components/common/PasswordInput';
import { useForm } from "react-hook-form";
import { useChangePassword } from "./profileMutations";
import { useProfile } from "./profileQueries";
import { getErrorMessage } from "../../../utils/errorMessage";

export default function AccountSettings() {
  const { data: profile } = useProfile();
  const { mutate, isLoading, error, isSuccess } = useChangePassword();

  const isGoogleUser = profile?.is_google_account;

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
    reset,
  } = useForm();

  const onSubmit = (data) => {
    mutate(
      {
        current_password: data.current_password,
        new_password: data.new_password,
        confirm_password: data.confirm_password,
      },
      {
        onSuccess: () => reset(),
      }
    );
  };

  return (
    <div className="flex-1 bg-surface-2 p-4 sm:p-6 lg:p-8">
      <div className="max-w-4xl mx-auto">
        <h1 className="text-[26px] font-bold text-fg mb-8">
          Account Settings
        </h1>

         <div className="bg-surface rounded-xl border p-6 mb-8">
          <div className="flex gap-4">
            <div className="w-10 h-10 bg-brand-soft flex items-center justify-center rounded-lg">
              <Mail className="w-5 h-5 text-brand" />
            </div>

            <div className="flex-1">
              <h2 className="font-semibold text-fg">Email Address</h2>
              <p className="text-sm text-fg-subtle mb-4">
                Your primary email for account access
              </p>

              <input
                value={profile?.email || ""}
                disabled
                className="w-full px-4 py-3 border rounded-lg bg-surface-3"
              />
            </div>
          </div>
        </div>

        {!isGoogleUser ? (
          <form
            onSubmit={handleSubmit(onSubmit)}
            className="bg-surface rounded-xl border p-6"
          >
            <div className="flex gap-4 mb-6">
              <div className="w-10 h-10 bg-warning-soft flex items-center justify-center rounded-lg">
                <Lock className="w-5 h-5 text-warning-soft-fg" />
              </div>

              <div>
                <h2 className="font-semibold text-fg">
                  Change Password
                </h2>
                <p className="text-sm text-fg-subtle">
                  Update your password securely
                </p>
              </div>
            </div>

            <div className="space-y-1 mb-4">
              <PasswordInput
                placeholder="Current password"
                aria-label="Current password"
                {...register("current_password", {
                  required: "Current password is required",
                })}
                className="w-full px-4 py-3 border rounded-lg"
              />
              {errors.current_password && (
                <p className="text-danger-soft-fg text-xs">
                  {errors.current_password.message}
                </p>
              )}
            </div>

            <div className="space-y-1 mb-4">
              {/* Three hand-rolled toggles driven by one `show` object, none of
                  them labelled for assistive tech. <PasswordInput> owns its own
                  visibility, so the three can no longer drift apart -- and a new
                  field added here gets a toggle by default. */}
              <PasswordInput
                placeholder="New password"
                aria-label="New password"
                showStrength
                {...register("new_password", {
                  required: "New password is required",
                  minLength: {
                    value: 8,
                    message: "Password must be at least 8 characters",
                  },
                })}
                className="w-full px-4 py-3 border rounded-lg"
              />
              {errors.new_password && (
                <p className="text-danger-soft-fg text-xs">
                  {errors.new_password.message}
                </p>
              )}
            </div>

            <div className="space-y-1 mb-6">
              <PasswordInput
                placeholder="Confirm new password"
                aria-label="Confirm new password"
                {...register("confirm_password", {
                  required: "Please confirm your password",
                  validate: (value) =>
                    value === watch("new_password") ||
                    "Passwords do not match",
                })}
                className="w-full px-4 py-3 border rounded-lg"
              />
              {errors.confirm_password && (
                <p className="text-danger-soft-fg text-xs">
                  {errors.confirm_password.message}
                </p>
              )}
            </div>

            {error && (
              <p className="text-danger-soft-fg text-sm mb-3">
                {getErrorMessage(error, "Password update failed")}
              </p>
            )}

            {isSuccess && (
              <p className="text-success text-sm mb-3">
                Password updated successfully
              </p>
            )}

            <div className="flex justify-end">
              <button
                type="submit"
                disabled={isLoading}
                className="px-8 py-3 bg-brand-soft text-brand-fg rounded-lg hover:bg-brand-hover disabled:opacity-50"
              >
                Save Changes
              </button>
            </div>
          </form>
        ) : (
          <div className="bg-surface rounded-xl border p-6">
            <div className="flex gap-4">
              <div className="w-10 h-10 rounded-lg bg-warning-soft flex items-center justify-center">
                <Lock className="w-5 h-5 text-warning" />
              </div>

              <div>
                <h2 className="font-semibold text-fg">
                  Password managed by Google
                </h2>
                <p className="text-sm text-fg-muted mt-1">
                  You signed in using Google. Your password is managed through
                  your Google account.
                </p>
                <a
                  href="https://myaccount.google.com/security"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-block mt-3 text-sm text-brand hover:underline"
                >
                  Manage Google Account →
                </a>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
