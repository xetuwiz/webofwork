import React from "react";
import { useSupportTickets } from "./supportQueries";
import { Loader2 } from "lucide-react";
import { Link } from "react-router-dom";
import Pagination from "../../../components/common/Pagination";
import EmptyState from "../../../components/common/EmptyState";
import { useListState, toQueryParams } from "../../../hooks/useListState";

export default function SupportTicketList() {
  // Page, page size and the status filter live in the URL, so the view is
  // shareable, Back steps back through filter changes rather than pages, and a
  // refresh keeps the user where they were.
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

  const statusFilter = filters.status || "All Status";

  const { data, isLoading, isError } = useSupportTickets(
    toQueryParams({ status: filters.status }, page, pageSize),
  );

  const getStatusBadgeClasses = (status) => {
    switch (status?.toLowerCase()) {
      case "open":
        return "bg-brand-soft text-brand px-3 py-1 rounded-full text-[11px] font-bold uppercase tracking-wider";
      case "resolved":
        return "bg-emerald-100 text-emerald-600 px-3 py-1 rounded-full text-[11px] font-bold uppercase tracking-wider";
      default:
        return "bg-surface-3 text-fg-muted px-3 py-1 rounded-full text-[11px] font-bold uppercase tracking-wider";
    }
  };
    const formatTicketId = (id) => {
  if (!id) return "";
  return `#TKT-${String(id).padStart(3, '0')}`;
};

  if (isError) return <div className="p-8 text-center text-danger-soft-fg font-bold">Failed to load tickets.</div>;

  return (
    <div className="min-h-screen bg-surface-2 py-4 md:py-8">
      <div className="max-w-page-2xl mx-auto px-4 md:px-8">
        <div className="flex flex-col md:flex-row md:items-center justify-between mb-8 gap-4">
          <h1 className="text-2xl md:text-[26px] font-black text-fg">
            Support Requests
          </h1>

          <div className="relative">
            <select
              value={statusFilter}
              aria-label="Filter tickets by status"
              onChange={(e) => setFilter("status", e.target.value)}
              className="w-full md:w-[200px] px-4 py-2.5 border border-line rounded-xl text-sm font-bold bg-surface focus-visible:ring-2 focus-visible:ring-primary outline-none appearance-none cursor-pointer shadow-sm"
            >
              <option>All Status</option>
              <option value="open">Open</option>
              <option value="resolved">Resolved</option>
            </select>
          </div>
        </div>

        {isLoading ? (
          <div className="flex justify-center py-20">
            <Loader2 className="w-10 h-10 animate-spin text-brand" />
          </div>
        ) : (
          <>
            <div className="bg-surface rounded-[20px] border border-line shadow-sm overflow-hidden hidden md:block">
              <div className="overflow-x-auto">
                <table className="w-full">
                  <thead>
                    <tr className="bg-surface-2/50 border-b border-line">
                      <th className="px-6 py-4 text-left text-[10px] font-black text-fg-subtle uppercase tracking-widest">
                        Ticket ID
                      </th>
                      <th className="px-6 py-4 text-left text-[10px] font-black text-fg-subtle uppercase tracking-widest">
                        Sender Role
                      </th>
                      <th className="px-6 py-4 text-left text-[10px] font-black text-fg-subtle uppercase tracking-widest">
                        Subject
                      </th>
                      <th className="px-6 py-4 text-left text-[10px] font-black text-fg-subtle uppercase tracking-widest">
                        Status
                      </th>
                      <th className="px-6 py-4 text-left text-[10px] font-black text-fg-subtle uppercase tracking-widest">
                        Date
                      </th>
                      <th className="px-6 py-4 text-left text-[10px] font-black text-fg-subtle uppercase tracking-widest">
                        Action
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100">
                    {data?.results?.map((ticket) => (
                      <tr key={ticket.id} className="hover:bg-surface-2/50 transition-colors">
                        <td className="px-6 py-4 text-sm font-bold text-fg">
                          {formatTicketId(ticket.id)}
                        </td>
                        <td className="px-6 py-4">
                          <span className="text-[11px] font-black uppercase text-fg-subtle bg-surface-3 px-2 py-0.5 rounded">
                            {ticket.sender_role}
                          </span>
                        </td>
                        <td className="px-6 py-4 text-sm text-fg-muted font-medium">
                          {ticket.subject}
                        </td>
                        <td className="px-6 py-4">
                          <span className={getStatusBadgeClasses(ticket.status)}>
                            {ticket.status}
                          </span>
                        </td>
                        <td className="px-6 py-4 text-sm text-fg-subtle">
                          {new Date(ticket.created_at).toLocaleDateString()}
                        </td>
                        <td className="px-6 py-4">
                          <Link to={`${ticket.id}`} className="px-4 py-2 text-xs font-black uppercase tracking-widest text-brand border-2 border-primary/10 rounded-xl hover:bg-brand-hover hover:text-brand-fg transition-all">
                            View
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

            </div>

            <div className="md:hidden space-y-4">
              {data?.results?.map((ticket) => (
                <div key={ticket.id} className="bg-surface rounded-2xl border border-line p-5 shadow-sm">
                  <div className="flex justify-between items-start mb-4">
                    <span className="text-sm font-black">#{ticket.id}</span>
                    <span className={getStatusBadgeClasses(ticket.status)}>{ticket.status}</span>
                  </div>
                  <h3 className="text-sm font-bold text-fg mb-1">{ticket.subject}</h3>
                  <p className="text-xs text-fg-subtle mb-4">{new Date(ticket.created_at).toLocaleDateString()}</p>
                  <button className="w-full py-3 bg-surface-2 text-[10px] font-black uppercase tracking-widest rounded-xl">View Ticket</button>
                </div>
              ))}
            </div>

            {/* One empty state for both layouts. Previously a zero-result search
                rendered an empty table on desktop and an empty gap on mobile,
                with no explanation in either. */}
            {(data?.results?.length ?? 0) === 0 ? (
              <EmptyState
                variant={hasActiveFilters ? "no-match" : "first-use"}
                title={hasActiveFilters ? "No tickets match this filter" : undefined}
                description={
                  hasActiveFilters
                    ? undefined
                    : "Support requests from clients and freelancers will appear here."
                }
                onClearFilters={resetFilters}
              />
            ) : null}

            {/* Deliberately outside the `hidden md:block` table wrapper. The
                old footer lived inside it, so on a phone the ticket list had no
                pager at all -- the second page was unreachable. */}
            <Pagination
              page={page}
              pageSize={pageSize}
              count={data?.count ?? 0}
              onPageChange={setPage}
              onPageSizeChange={setPageSize}
              itemNoun="tickets"
            />
          </>
        )}
      </div>
    </div>
  );
}