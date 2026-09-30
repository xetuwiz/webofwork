import { Filter, Loader2, ChevronDown } from "lucide-react";
import { usePlatformTransactions } from "./transactionQuery";
import Pagination from "../../../components/common/Pagination";
import EmptyState from "../../../components/common/EmptyState";
import { Popover, PopoverTrigger, PopoverContent, PopoverItem } from "../../../components/common/Popover";
import { useListState, toQueryParams } from "../../../hooks/useListState";

export default function AdminTransactions() {
    // Page, page size and the status filter live in the URL, so a specific
    // transaction view is shareable and Back steps back through filter changes
    // rather than pages.
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
    const statusFilter = filters.status;

    const { data: payments, isLoading } = usePlatformTransactions(
        toQueryParams({ status: statusFilter }, page, pageSize),
    );

    const statuses = [
        { label: "All Transactions", value: "" },
        { label: "Pending", value: "pending" },
        { label: "Escrow", value: "escrow" },
        { label: "Released", value: "released"},
        { label: "Refunded", value: "refunded" }
    ];

    // The click-outside listener, the ref and the open boolean are all gone:
    // <Popover> closes on outside pointer-down and on Escape, and returns focus
    // to its trigger on close.

    const getStatusStyles = (status) => {
        switch (status?.toLowerCase()) {
            case "released":
            case 'completed':
                return "bg-success-soft text-success";
            case "pending":
            case "escrow":
                return "bg-warning-soft text-warning";
            case "refunded":
                return "bg-danger-soft text-danger";
            default:
                return "bg-surface-2 text-fg-muted";
        }
    };

    const handleSelectStatus = (value) => {
        // `set` drops `page` alongside the filter, so this is the whole of
        // "changing a filter returns to page 1". <Popover> closes itself.
        setFilter("status", value);
    };

    if (isLoading) {
        return (
            <div className="min-h-screen flex items-center justify-center">
                <Loader2 className="w-8 h-8 animate-spin text-brand" />
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-surface w-full overflow-hidden">
            <div className="px-4 sm:px-6 lg:px-12 py-8 sm:py-12">
                <div className="mb-8 sm:mb-12">
                    <h1 className="text-2xl sm:text-[26px] font-semibold text-fg leading-9 mb-2">
                        Transactions
                    </h1>
                    <p className="text-xs sm:text-[13.6px] font-light text-fg-subtle leading-[26px]">
                        View and manage all payment transactions
                    </p>
                </div>

                {/* Was a useState boolean plus a click-outside `mousedown`
                    listener and a conditional div. It closed on outside click but
                    not on Escape, and never returned focus to the trigger, so a
                    keyboard user could tab into the page behind an open menu and
                    had no keyboard way to close it. */}
                <div className="flex justify-end mb-8 sm:mb-12">
                    <Popover>
                        <PopoverTrigger className="flex items-center gap-2 px-4 sm:px-6 py-2.5 sm:py-3 border border-line rounded-lg hover:bg-surface-2 transition-colors capitalize min-w-[160px] justify-between">
                            <div className="flex items-center gap-2">
                                <Filter className="w-4 sm:w-5 h-4 sm:h-5 text-fg-subtle" strokeWidth={1.5} aria-hidden="true" />
                                <span className="text-sm text-fg">
                                    {statuses.find((s) => s.value === statusFilter)?.label || "All"}
                                </span>
                            </div>
                            <ChevronDown
                                className="w-4 h-4 text-fg-subtle transition-transform group-data-[state=open]:rotate-180"
                                aria-hidden="true"
                            />
                        </PopoverTrigger>
                        <PopoverContent aria-label="Filter by status" align="end" className="w-56">
                            {statuses.map((status) => (
                                <PopoverItem
                                    key={status.value}
                                    onClick={() => handleSelectStatus(status.value)}
                                    aria-current={statusFilter === status.value ? "true" : undefined}
                                    className={
                                        statusFilter === status.value
                                            ? "bg-brand-soft text-brand font-medium"
                                            : ""
                                    }
                                >
                                    {status.label}
                                </PopoverItem>
                            ))}
                        </PopoverContent>
                    </Popover>
                </div>

                {/* Contextual. With a status filter applied, an empty list means
                    that status has no transactions -- not that the platform has
                    never processed any. */}
                {(payments?.results?.length ?? 0) === 0 ? (
                    <EmptyState
                        variant={hasActiveFilters ? "no-match" : "first-use"}
                        title={hasActiveFilters ? "No transactions with this status" : undefined}
                        onClearFilters={resetFilters}
                    />
                ) : null}

                <div className="hidden lg:block">
                    <div className="overflow-x-auto border border-line-soft rounded-lg">
                        <table className="w-full min-w-[900px]">
                            <thead>
                                <tr className="border-b border-line-soft bg-surface-2/30">
                                    <th className="text-left px-4 xl:px-8 py-4 text-[10.2px] font-medium text-fg-subtle uppercase tracking-wider">Payment ID</th>
                                    <th className="text-left px-4 xl:px-8 py-4 text-[10.2px] font-medium text-fg-subtle uppercase tracking-wider">Client</th>
                                    <th className="text-left px-4 xl:px-8 py-4 text-[10.2px] font-medium text-fg-subtle uppercase tracking-wider">Freelancer</th>
                                    <th className="text-left px-4 xl:px-8 py-4 text-[10.2px] font-medium text-fg-subtle uppercase tracking-wider">Amount</th>
                                    <th className="text-left px-4 xl:px-8 py-4 text-[10.2px] font-medium text-fg-subtle uppercase tracking-wider">Platform Fee</th>
                                    <th className="text-left px-4 xl:px-8 py-4 text-[10.2px] font-medium text-fg-subtle uppercase tracking-wider">Status</th>
                                    <th className="text-left px-4 xl:px-8 py-4 text-[10.2px] font-medium text-fg-subtle uppercase tracking-wider">Date</th>
                                </tr>
                            </thead>
                            <tbody>
                                {payments?.results?.map((transaction) => (
                                    <tr key={transaction.id} className="border-b border-line-soft hover:bg-surface-2/50">
                                        <td className="px-4 xl:px-8 py-5 text-sm font-medium">{transaction.payment_id}</td>
                                        <td className="px-4 xl:px-8 py-5 text-sm">{transaction.client_name}</td>
                                        <td className="px-4 xl:px-8 py-5 text-sm">{transaction.freelancer_name}</td>
                                        <td className="px-4 xl:px-8 py-5 text-sm font-semibold">${transaction.amount_total}</td>
                                        <td className="px-4 xl:px-8 py-5 text-sm text-fg-muted">${transaction.platform_fee}</td>
                                        <td className="px-4 xl:px-8 py-5">
                                            <span className={`inline-flex items-center px-3 py-1 rounded-md text-xs font-medium capitalize ${getStatusStyles(transaction.status)}`}>
                                                {transaction.status}
                                            </span>
                                        </td>
                                        <td className="px-4 xl:px-8 py-5 text-sm text-fg-subtle">{transaction.date}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                </div>

                <div className="lg:hidden space-y-4">
                    {payments?.results?.map((transaction) => (
                        <div key={transaction.id} className="border border-line-soft rounded-lg p-4">
                            <div className="flex justify-between mb-3">
                                <span className="text-sm font-medium">{transaction.payment_id}</span>
                                <span className={`px-2 py-1 rounded text-xs capitalize ${getStatusStyles(transaction.status)}`}>{transaction.status}</span>
                            </div>
                            <div className="grid grid-cols-2 gap-2 text-sm">
                                <div className="text-fg-subtle">Client: {transaction.client_name}</div>
                                <div className="text-fg font-bold text-right">${transaction.amount_total}</div>
                            </div>
                        </div>
                    ))}
                </div>

                {/* Replaces a hand-rolled prev/next bar whose counter read
                    `payments.results.length` -- this page's row count, not the
                    range start -- so on page 3 it said "Showing 10 of 42". */}
                <Pagination
                    page={page}
                    pageSize={pageSize}
                    count={payments?.count ?? 0}
                    onPageChange={setPage}
                    onPageSizeChange={setPageSize}
                    itemNoun="transactions"
                    className="mt-8 pt-0"
                />
            </div>
        </div>
    );
}
