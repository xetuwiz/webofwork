import React from "react";
import ProjectHeader from "./components/ProposalHeader";
import ProposalCard from "./components/ProposalsCard";
import { useParams } from "react-router-dom";
import { useProjectDetails } from "../projectQueries";
import { useProposals, useSentInvitations } from "./proposalsQueries";
import { useRejectProposal } from "./proposalsMutations";
import Pagination from "../../../../components/common/Pagination";
import EmptyState from "../../../../components/common/EmptyState";
import { useListState } from "../../../../hooks/useListState";

export default function ProposalsIndex() {
  const { id } = useParams();

  // Status, page and page size live in the URL, so a view of the proposals for a
  // project is shareable and Back steps back through filter changes.
  const {
    page,
    pageSize,
    filters,
    set: setFilter,
    setPage,
    setPageSize,
    hasActiveFilters,
    resetFilters,
  } = useListState({ defaults: { status: "" } });
  const status = filters.status;

  const { data: project, isLoading: isProjectLoading } = useProjectDetails(id);
  const { data: invitationsData, isLoading: isInvitesLoading } = useSentInvitations(id);
  const { data: proposalsData, isLoading: isProposalsLoading } = useProposals(id, {
    page,
    page_size: pageSize,
    status,
  });
  const { mutate: rejectMutation, isPending: isRejecting } = useRejectProposal();

  if (isProjectLoading || isInvitesLoading || isProposalsLoading) {
    return <div className="p-10 text-center font-inter text-fg-muted">Loading data...</div>;
  }

  const invitations = invitationsData?.results || (Array.isArray(invitationsData) ? invitationsData : []);
  const actualProposals = proposalsData?.results || [];
  
  const sortedProposals = [...actualProposals].sort((a, b) => {
    if (a.contract_info?.is_this_freelancer) return -1;
    if (b.contract_info?.is_this_freelancer) return 1;
    return 0;
  });
console.log(sortedProposals);

  const anyOfferMade = actualProposals.some(p => p.contract_info !== null);

  const displayBudget = project?.pricing_type === "fixed" 
    ? `$${Number(project.fixed_price).toLocaleString()}` 
    : `$${Number(project.min_budget).toLocaleString()} - $${Number(project.max_budget).toLocaleString()}`;

  const tabs = [
    { label: "All Proposals", value: "", count: project?.proposals_count || actualProposals.length },
    { label: "Pending", value: "pending" },
    { label: "Rejected", value: "rejected" },
  ];

  return (
    <div className="min-h-screen bg-surface-2">
      <div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        
        <div className="mb-8">
          <ProjectHeader 
            title={project?.title}
            budget={displayBudget}
            proposalCount={proposalsData?.count || 0}
            postedTime={`Posted ${new Date(project?.created_at).toLocaleDateString()}`}
          />
        </div>

        {invitations.length > 0 && status === "" && (
          <div className="mb-10">
            <h2 className="text-xl font-bold text-fg mb-4 flex items-center gap-2">
              Sent Invitations
              <span className="bg-brand-soft text-brand text-xs px-2 py-1 rounded-full">{invitations.length}</span>
            </h2>
            <div className="space-y-4">
              {invitations.map((inv) => (
                <ProposalCard
                  key={inv.id}
                  isInvitation={true}
                  invitationStatus={inv.status}
                  anyOfferMade={anyOfferMade}
                  freelancer={{
                    id: inv.freelancer,
                    name: inv.freelancer_name, 
                    title: inv.freelancer_tagline || "Invited Talent",
                    image: inv.freelancer_image ,
                  }}
                  proposal={{
                    title: project?.title,       
                    projectId: id,               
                    freelancerId: inv.freelancer_id, 
                    description: inv.message || "No message.",
                    bidAmount: inv.package_amount,
                    contract_info: inv.contract_info
                  }}
                />
              ))}
            </div>
          </div>
        )}

        <div className="mb-6 border-b border-line">
          <div className="flex gap-8">
            {tabs.map((tab) => (
              <button
                key={tab.value}
                onClick={() => setFilter("status", tab.value)}
                className={`pb-4 text-sm font-semibold transition-all ${
                  status === tab.value 
                  ? "border-b-2 border-blue-500 text-brand" 
                  : "text-fg-subtle hover:text-fg-muted"
                }`}
              >
                {tab.label}
              </button>
            ))}
          </div>
        </div>

        <div className="mb-8 space-y-6">
          {sortedProposals.length > 0 ? (
            sortedProposals.map((prop) => (
              <ProposalCard
                key={prop.id}
                isInvitation={false}
                invitationStatus={prop.status}
                anyOfferMade={anyOfferMade}
                onReject={() => rejectMutation(prop.id)}
                isRejecting={isRejecting}
                freelancer={{
                  id: prop.freelancer_id,
                  name: prop.freelancer_name,
                  title: prop.freelancer_tagline || "Freelancer",
                  image: prop.freelancer_photo ,
                  rating: prop.freelancer_ratings, 
                }}
                proposal={{
                  title: project?.title,
                  projectId: id,
                  freelancerId: prop.freelancer,
                  description: prop.cover_letter,
                  bidAmount: prop.bid_amount,
                  deliveryDays: prop.delivery_days,
                  contract_info: prop.contract_info
                }}
              />
            ))
          ) : (
            <EmptyState
              variant={hasActiveFilters ? "no-match" : "first-use"}
              title={hasActiveFilters ? "No proposals found for this filter" : undefined}
              onClearFilters={resetFilters}
              className="bg-surface rounded-2xl border-2 border-dashed border-line-soft"
            />
          )}
        </div>

        {/* Replaces a duplicated mobile/desktop footer pair. Its counter
            hardcoded a page size of 10 -- `{(page - 1) * 10 + 1}` -- while the
            endpoint serves 20 per page, so on page 2 it claimed to be showing
            rows 11-20 of a list whose rows 11-20 were not on screen. */}
        <Pagination
          page={page}
          pageSize={pageSize}
          count={proposalsData?.count ?? 0}
          onPageChange={setPage}
          onPageSizeChange={setPageSize}
          itemNoun="proposals"
          className="mt-4 pt-0"
        />
      </div>
    </div>
  );
}