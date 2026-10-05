import React, { useState } from 'react';
import { X, ChevronDown, Loader2 } from 'lucide-react';
import { useEligibleProjects, useSendInvitation} from '../findTalentQueries'
import Dialog from "../../../../components/common/Dialog";
import { toastApiError, toastSuccess, toastWarning } from "../../../../utils/toast";
import Button from "../../../../components/common/Button";
import { Field } from "../../../../components/common/Field";

export default function InviteFreelancerModal({ isOpen, onClose, modalProps }) {
  const { freelancerId, packages } = modalProps ;
  const [selectedProjectId, setSelectedProjectId] = useState('');
  const [selectedPackageId, setSelectedPackageId] = useState(null);
  const [message, setMessage] = useState('');

  const { data: projects, isLoading: projectsLoading } = useEligibleProjects(isOpen);
   
    
 const inviteMutation = useSendInvitation({
    onSuccess: () => {
      toastSuccess("Invitation sent successfully!");
      onClose();
    },
    onError: (error) => {
      // `getErrorMessage` handles every shape the API uses -- `error`, `detail`,
      // `message`, a validation `errors` dictionary, an array of strings. This
      // used to check only the first two, so a FluentValidation rejection came
      // back as the generic fallback with the field-level reason discarded.
      toastApiError(error, "Failed to send invitation. Please try again.");
    }

  });
  

const handleSendInvite = () => {
    if (!selectedProjectId || !selectedPackageId) {
      toastWarning("Please select a project and a package");
      return;
    }
    
    inviteMutation.mutate({
      // These are GUIDs, not integers: parseInt("<guid>") returns NaN, which serialises to
      // null and cannot bind to the backend's Guid fields.
      freelancer: freelancerId,
      project: parseInt(selectedProjectId, 10),
      package: selectedPackageId,
      message: message
    }, {
      onSuccess: () => {
        onClose();
        setSelectedProjectId('');
        setSelectedPackageId(null);
        setMessage('');
      },
      onError: (error) => {
        // This handler computed a message and then discarded it -- no toast, no
        // log. A failed invite was completely invisible: the modal stayed open
        // with the form still filled and nothing to explain why.
        toastApiError(error, "Failed to send invitation. Please try again.");
      }
    });
  };
  

  if (!isOpen) return null;

  return (
    <Dialog
      open={isOpen}
      onClose={onClose}
      title="Send Invitation"
      size="sm"
      className="overflow-hidden flex flex-col"
    >
        <div className="p-8 border-b border-line-soft flex justify-between items-start">
          <div>
            <h2 className="text-2xl font-black text-fg tracking-tight">Send Invitation</h2>
            <p className="text-sm text-fg-subtle mt-1">Select a project to invite this talent.</p>
          </div>
          <button onClick={onClose} className="p-2 hover:bg-surface-3 rounded-full transition-colors">
            <X className="w-5 h-5 text-fg-subtle" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-8 space-y-8">
          <div>
            <Field label="Choose a Job" required>
              {(field) => (
                <div className="relative">
                  <select
                    {...field}
                    value={selectedProjectId}
                    onChange={(e) => setSelectedProjectId(e.target.value)}
                    className="w-full h-14 pl-5 pr-12 bg-surface-2 border border-line-soft rounded-2xl text-sm font-bold text-fg focus-visible:ring-4 focus-visible:ring-brand/20 focus-visible:border-brand outline-none appearance-none transition-all cursor-pointer"
                  >
                    <option value="">Select a job...</option>
                    {projects?.map((project) => (
                      <option key={project.id} value={project.id}>{project.title}</option>
                    ))}
                  </select>
                  <ChevronDown className="absolute right-5 top-1/2 -translate-y-1/2 w-5 h-5 text-fg-subtle pointer-events-none" aria-hidden="true" />
                </div>
              )}
            </Field>
            {projectsLoading && <p className="text-xs text-brand mt-2 animate-pulse font-medium">Loading your projects...</p>}
          </div>

          <div>
            {/* A group of mutually exclusive choices, so a fieldset. Each card
                was a bare <button> whose selected state was conveyed by border
                colour alone -- `aria-pressed` exposes it to assistive tech. The
                same pattern as StepTwo's experience levels. */}
            <fieldset>
              <legend className="text-xs font-black uppercase tracking-widest text-fg-subtle mb-4">Select Package</legend>
              <div className="grid grid-cols-1 gap-3">
                {packages.map((pkg) => (
                  <button
                    key={pkg.package_type}
                    type="button"
                    aria-pressed={selectedPackageId === pkg.id}
                    onClick={() => setSelectedPackageId(pkg.id )}
                    className={`p-5 rounded-2xl border-2 text-left transition-all flex justify-between items-center ${
                      selectedPackageId === pkg.id
                      ? 'border-blue-600 bg-brand-soft shadow-md shadow-blue-100'
                      : 'border-line-soft bg-surface hover:border-line'
                    }`}
                  >
                    <div>
                      <h4 className="font-bold text-fg capitalize">{pkg.package_type} Package</h4>
                      <p className="text-xs text-fg-subtle mt-1">{pkg.delivery_days} Days Delivery</p>
                    </div>
                    <span className="text-lg font-black text-brand">${pkg.price}</span>
                  </button>
                ))}
              </div>
            </fieldset>
          </div>

          <div>
            {/* "Optional" was inside the label text, so it became part of the
                field's accessible name rather than a note about it. */}
            <Field label="Optional Message" hint="Optional">
              {(field) => (
                <textarea
                  {...field}
                  rows={4}
                  value={message}
                  onChange={(e) => setMessage(e.target.value)}
                  className="w-full p-5 bg-surface-2 border border-line-soft rounded-2xl text-sm outline-none focus-visible:ring-4 focus-visible:ring-brand/20 focus-visible:border-brand transition-all resize-none"
                  placeholder="Tell them why they're a good fit..."
                />
              )}
            </Field>
          </div>
        </div>

        <div className="p-8 border-t border-line-soft flex gap-4">
          <button onClick={onClose} className="flex-1 py-4 font-bold text-fg-subtle hover:bg-surface-2 rounded-2xl transition-colors">
            Cancel
          </button>
                    <Button
            onClick={handleSendInvite}
            variant="primary"
            size="none"
            loading={inviteMutation.isPending}
            className="flex-1 py-4 rounded-2xl shadow-xl shadow-blue-200"
          >
            Send Invitation
          </Button>
        </div>
    </Dialog>
  );
}
