import React, { useEffect, useState } from 'react';
import { CheckCircle2, Clock, ShieldCheck, X, Zap, SendHorizontal, Info } from 'lucide-react';
import { useFreelancerProfile } from '../../profile/profileQueries';
import { useSubmitProposal } from './proposalsMutations';
import { fetchPlatformFee } from './proposalsApi';
import Dialog from "../../../../components/common/Dialog";
import { toastError } from "../../../../utils/toast";
import { Field } from "../../../../components/common/Field";

const PackageCard = ({ type, data, isSelected, onClick }) => {
  if (!data) return null;

  return (
    <button
      type="button"
      onClick={onClick}
      className={`relative w-full p-4 md:p-5 rounded-2xl border-2 transition-all duration-slow text-left group ${
        isSelected
          ? 'border-primary bg-surface shadow-lg shadow-primary/5'
          : 'border-line bg-surface hover:border-line-strong'
      }`}
    >
      {isSelected && (
        <div className="absolute -top-2 -right-2 bg-brand text-brand-fg p-1 rounded-full shadow-lg z-10">
          <CheckCircle2 className="w-3.5 h-3.5" />
        </div>
      )}
      
      <div className="flex justify-between items-start mb-2">
        <div>
          <span className={`text-[9px] font-black uppercase tracking-widest ${isSelected ? 'text-brand' : 'text-fg-subtle'}`}>
            {type === 'starter' ? 'Standard' : 'Premium'} Tier
          </span>
          <div className="text-lg font-black text-fg mt-0.5">
            ${data.price.toLocaleString()}
          </div>
        </div>
        <div className="flex items-center gap-1 px-2 py-1 bg-surface-3 rounded-lg group-hover:bg-surface-inset transition-colors">
          <Clock className="w-3 h-3 text-fg-subtle" />
          <span className="text-[9px] font-bold text-fg-muted uppercase tracking-tight">{data.delivery_days} Days</span>
        </div>
      </div>
      
      <ul className="space-y-1.5">
        {data.description.slice(0, 3).map((line, i) => (
          <li key={i} className="flex items-center gap-2 text-[9px] font-bold text-fg-subtle uppercase leading-tight">
            <CheckCircle2 className={`w-3 h-3 shrink-0 ${isSelected ? 'text-brand' : 'text-fg-subtle'}`} />
            <span className="truncate">{line}</span>
          </li>
        ))}
      </ul>
    </button>
  );
};

export function ProposalModal({ onClose, projectId }) {
  const [coverLetter, setCoverLetter] = useState('');
  const [selectedPackage, setSelectedPackage] = useState('starter');
  const [feePercentage, setFeePercentage] = useState(10);
  const { data: profile, isLoading } = useFreelancerProfile();
  const { mutate, isPending } = useSubmitProposal();

useEffect(() => {
    const getFee = async () => {
      try {
        const fee = await fetchPlatformFee();
        setFeePercentage(Number(fee));
      } catch (error) {
        console.error("Failed to fetch fee:", error);
      }
    };
    getFee();
  }, []);
  // A loading state, not a modal: no scrim click-to-close, no focus trap, and
  // nothing to dismiss. It matched the `fixed inset-0 z-modal` scrim scan only
  // because it centres a spinner over the viewport, which is also what a modal
  // scrim does. The spinner is marked decorative -- a bare spinning ring is
  // announced as nothing useful, and the dialog's own title and description
  // already say what is loading.
  if (isLoading) return (
    <div className="fixed inset-0 z-modal flex items-center justify-center bg-inverse/40 backdrop-blur-sm">
        <div className="w-12 h-12 border-4 border-primary border-t-transparent rounded-full animate-spin" aria-hidden="true"></div>
    </div>
  );

  if (!profile) return null;

  // `packages` is always an object -- the endpoint returns
  // `{ starter: {...} | null, pro: {...} | null }`, with null for tiers the
  // freelancer has not created. The old guard was
  // `if (!profile || !profile.packages) return null`, which therefore never
  // fired and guarded nothing.
  const packages = profile.packages ?? {};
  const availablePackages = ['starter', 'pro'].filter((t) => packages[t]);
  const currentPkgData = packages[selectedPackage] ?? null;

  const handleSubmit = () => {
    if (!coverLetter.trim()) return toastError("Please write a cover letter");
    // Read `currentPkgData.id` with no null check, so a freelancer whose
    // `starter` tier is missing got an unhandled TypeError on click.
    if (!currentPkgData) return toastError("Choose a service tier first");

    const proposalData = {
      project: projectId, 
      package: currentPkgData.id, 
      cover_letter: coverLetter,
    };

    mutate(proposalData, { onSuccess: () => onClose() });
  };

  // No tier configured. The API refuses a proposal without a package
  // ("Invalid package or you do not own it."), so this is not a rare edge case
  // -- it is where every new freelancer starts. Say so and point at the screen
  // that fixes it, rather than rendering on a null.
  if (availablePackages.length === 0) {
    return (
      <Dialog
        open
        onClose={onClose}
        title="Package unavailable"
        size="md"
      >
          <button onClick={onClose} className="absolute top-6 right-6 p-2 text-fg-subtle hover:text-fg rounded-full transition-all">
            <X className="w-5 h-5" />
          </button>
          <div className="w-14 h-14 rounded-2xl bg-warning-soft text-warning-soft-fg flex items-center justify-center mx-auto mb-6">
            <ShieldCheck className="w-7 h-7" />
          </div>
          <h2 className="text-xl font-black text-fg tracking-tight mb-3">
            Create a package first
          </h2>
          <p className="text-sm text-fg-subtle font-medium leading-relaxed mb-8">
            Proposals are sent against one of your service packages, so you need a
            Standard or Premium tier before you can apply to a project.
          </p>
          <a
            href="/dashboard/profile"
            className="inline-flex h-12 items-center justify-center px-8 rounded-xl bg-brand text-brand-fg text-xs font-black uppercase tracking-widest shadow-lg shadow-primary/20 transition-all hover:opacity-95"
          >
            Go to profile
          </a>
      </Dialog>
    );
  }

  // `selectedPackage` can still point at a tier the freelancer has since
  // deleted, so the money panel and the submit button fall back to a tier that
  // actually exists.
  const activePackage = currentPkgData ?? packages[availablePackages[0]];

  return (
    <Dialog
      open
      onClose={onClose}
      title="Send Proposal"
      size="xl"
      className="overflow-hidden flex flex-col"
    >
        
        <div className="flex lg:hidden items-center justify-between p-5 border-b border-line-soft bg-surface sticky top-0 z-20">
            <div className="flex items-center gap-2">
                <Zap className="w-4 h-4 text-brand fill-primary" />
                <span className="text-[10px] font-black uppercase tracking-widest text-fg">Proposal</span>
            </div>
            <button onClick={onClose} className="p-2 bg-surface-2 rounded-full">
                <X className="w-5 h-5 text-fg-subtle" />
            </button>
        </div>

        <div className="flex-1 overflow-y-auto p-6 md:p-10 lg:p-14 scrollbar-hide">
          <div className="hidden lg:block mb-8">
            <div className="flex items-center gap-2 mb-2">
              <Zap className="w-4 h-4 text-brand fill-primary" />
              <span className="text-[10px] font-black text-brand uppercase tracking-[0.2em]">Project Application</span>
            </div>
            <h1 className="text-3xl font-black text-fg tracking-tight">
              Submit Proposal
            </h1>
            <p className="text-sm text-fg-subtle mt-2 font-medium">
              Highlight your relevant experience and project approach.
            </p>
          </div>

          <div className="space-y-4">
            {/* The char counter sits beside the label, so the pairing is
                preserved by rendering it as the Field's `hint` sibling rather
                than by keeping a flex row around a <label> that named nothing. */}
            <Field label="Executive Summary / Cover Letter">
              {(f) => (
                <>
                  <div className="flex justify-between items-center">
                    <span className="text-[10px] font-bold text-fg-subtle">{coverLetter.length} chars</span>
                  </div>
                  <textarea
                    value={coverLetter}
                    onChange={(e) => setCoverLetter(e.target.value)}
                    placeholder="Explain why you are the best fit for this role..."
                    {...f}
                    className="w-full min-h-[250px] lg:min-h-[400px] px-6 py-5 border-2 border-line-soft rounded-3xl text-fg-muted leading-relaxed focus-visible:border-primary focus-visible:ring-8 focus-visible:ring-primary/5 outline-none transition-all resize-none bg-surface-2/50 placeholder:text-fg-subtle text-sm md:text-base"
                  />
                </>
              )}
            </Field>
          </div>
        </div>

        <div className="w-full lg:w-[420px] bg-surface-2/80 border-t lg:border-t-0 lg:border-l border-line p-6 md:p-8 lg:p-12 flex flex-col overflow-y-auto max-h-[50vh] lg:max-h-full">
          
          <button 
            onClick={onClose} 
            className="hidden lg:flex absolute top-8 right-8 p-2 text-fg-subtle hover:text-fg hover:bg-surface rounded-full transition-all"
          >
            <X className="w-5 h-5" />
          </button>

          <div className="mb-6">
            <h2 className="text-[10px] font-black text-fg-subtle uppercase tracking-[0.2em] mb-4">
                Service Level Selection
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-1 gap-3">
                <PackageCard 
                    type="starter" 
                    data={profile.packages.starter} 
                    isSelected={selectedPackage === 'starter'} 
                    onClick={() => setSelectedPackage('starter')}
                />
                <PackageCard 
                    type="pro" 
                    data={profile.packages.pro} 
                    isSelected={selectedPackage === 'pro'} 
                    onClick={() => setSelectedPackage('pro')}
                />
            </div>
          </div>

          <div className="bg-surface rounded-[2rem] p-6 border border-line shadow-sm space-y-4">
            <h3 className="text-[10px] font-black text-fg-subtle uppercase tracking-widest border-b border-line-soft pb-3">Financial Breakdown</h3>
            <div className="space-y-3">
                <div className="flex justify-between text-[11px] font-bold text-fg-subtle">
                    <span className="uppercase tracking-tight">Contract Value</span>
                    <span className="text-fg">${Number(activePackage?.price ?? 0).toLocaleString()}</span>
                </div>
                <div className="flex justify-between text-[11px] font-bold text-fg-subtle">
                    <span className="flex items-center gap-1 uppercase tracking-tight">Platform Fee <Info className="w-3 h-3 text-fg-subtle"/></span>
                    <span className="text-danger-soft-fg">-${((activePackage?.price ?? 0) * (feePercentage/100)).toLocaleString()}</span>
                </div>
                <div className="h-px bg-surface-2 my-1" />
                <div className="flex justify-between items-center">
                    <span className="text-[11px] font-black text-fg uppercase tracking-widest">Net Earnings</span>
                    {/* Hard-coded to `price * 0.9` while the fee line directly above
                        it used the fetched `feePercentage`, so the two disagreed the
                        moment the configured platform fee was not exactly 10%. */}
                    <span className="text-xl font-black text-emerald-600">${((activePackage?.price ?? 0) * (1 - feePercentage/100)).toLocaleString()}</span>
                </div>
            </div>
          </div>

          <div className="mt-8">
            <button
              onClick={handleSubmit}
              disabled={isPending}
              className="w-full h-14 md:h-16 text-[11px] font-black text-brand-fg bg-brand rounded-2xl shadow-xl shadow-primary/20 hover:opacity-95 active:scale-[0.98] transition-all flex items-center justify-center gap-3 uppercase tracking-widest disabled:opacity-50"
            >
              {isPending ? (
                <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
              ) : (
                <>
                  <SendHorizontal className="w-4 h-4" />
                  Send Proposal
                </>
              )}
            </button>
          </div>
        </div>
    </Dialog>
  );
}
