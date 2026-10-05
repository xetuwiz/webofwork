import React, { useState } from 'react';
import { 
  MessageSquare, 
  Clock, 
  Package, 
  Hash, 
  Search,
  ArrowUpRight,
  User,
  Calendar,
  X
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useMyContracts } from '../../freelancer/contracts/contractsQueries';
import { useChatActions } from '../../../components/chat/chatMutations';
import { useModal } from "../../../hooks/modal/useModalStore";
import { formatDateDMY } from '../../../utils/date';
import { toastError } from "../../../utils/toast";
import Pagination from "../../../components/common/Pagination";
import EmptyState from "../../../components/common/EmptyState";
import { useListState, toQueryParams } from "../../../hooks/useListState";

export default function ClientContracts() {
  // Tab, page, page size and the search term all live in the URL, so a filtered
  // view is shareable and Back steps back through tab changes rather than pages.
  const {
    page,
    pageSize,
    filters,
    set: setFilter,
    setPage,
    setPageSize,
    hasActiveFilters,
    resetFilters,
  } = useListState({ defaults: { tab: 'All', search: '' } });

  const activeTab = filters.tab || 'All';

  // Seeded from the URL so a shared link shows the term it was shared with.
  const [searchInput, setSearchInput] = useState(filters.search);
  const [lastSynced, setLastSynced] = useState(filters.search);
  if (filters.search !== lastSynced) {
    setLastSynced(filters.search);
    setSearchInput(filters.search);
  }

  const navigate = useNavigate();
  const { getRoomMutation } = useChatActions();
  const { openModal } = useModal();

  const tabs = ['All', 'In Progress', 'Submitted', 'Completed', 'Disputed'];

  // Maps each tab to a real ContractStatus. The old code sent the raw tab label,
  // and ContractsController does `Enum.TryParse<ContractStatus>(status)` -- so
  // 'All' and 'In Progress' both failed to parse and silently became NO filter.
  // The "In Progress" tab therefore showed every contract, not just active ones.
  const STATUS_FOR_TAB = {
    All: '',
    'In Progress': 'Active',
    Submitted: 'Submitted',
    Completed: 'Completed',
    Disputed: 'Disputed',
  };

  // Declared before the hook: reading it from the query would be a
  // temporal-dead-zone crash (the same bug FindTalent.jsx had).
  const { data, isLoading } = useMyContracts(
    toQueryParams(
      { status: STATUS_FOR_TAB[activeTab], role: 'client', search: filters.search },
      page,
      pageSize,
    ),
  );

  const handleSearchTrigger = () => setFilter('search', searchInput.trim());

  const handleClearSearch = () => {
    setSearchInput('');
    setFilter('search', '');
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') {
      handleSearchTrigger();
    }
  };

  const handleMessageFreelancer = async (freelancerId) => {
    try {
      const room = await getRoomMutation.mutateAsync(freelancerId);
      openModal("messages", { initialRoomId: room.id });
    } catch {
      toastError("Could not open chat room.");
    }
  };

  const calculateDeadline = (signedAt, deliveryDays) => {
    if (!signedAt) return "Pending Signature";
    const date = new Date(signedAt);
    date.setDate(date.getDate() + (Number(deliveryDays) || 0));
    return formatDateDMY(date.toISOString());
  };

  if (isLoading) return (
    <div className="min-h-screen bg-surface-2 flex items-center justify-center">
      <div className="flex flex-col items-center gap-4">
        <div className="w-12 h-12 border-4 border-blue-600 border-t-transparent rounded-full animate-spin"></div>
        <p className="text-fg-subtle font-bold tracking-widest text-xs uppercase">Initializing Registry</p>
      </div>
    </div>
  );

  const contractList = data?.results || [];
  const totalCount = data?.count || 0;

  // `set` drops `page` alongside the filter, so this is the whole of
  // "changing a tab returns to page 1".
  const handleTabChange = (tab) => setFilter('tab', tab);

  return (
    <div className="min-h-screen bg-surface-2 pb-20 font-sans">
      <div className="bg-surface border-b border-line">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 py-10 md:py-16">
          <div className="flex flex-col md:flex-row md:items-end justify-between gap-6">
            <div>
              <h1 className="text-3xl md:text-5xl font-black text-fg tracking-tight">Contracts</h1>
              <p className="text-base md:text-lg text-fg-subtle mt-1 font-medium">Total: {totalCount} agreements.</p>
            </div>
            
            <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3 w-full md:w-auto">
              <div className="relative group flex-1 sm:flex-none">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-fg-subtle group-focus-within:text-brand transition-colors" />
                <input 
                  type="text" 
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  onKeyDown={handleKeyDown}
                  placeholder="Search project or ID..." 
                  className="pl-10 pr-10 py-3 bg-surface-2 border border-line rounded-xl text-sm outline-none focus-visible:ring-4 focus-visible:ring-brand/10 focus-visible:border-brand w-full sm:w-64 md:w-80 transition-all"
                />
                {searchInput && (
                  <button 
                    onClick={handleClearSearch}
                    className="absolute right-3 top-1/2 -translate-y-1/2 p-1 hover:bg-surface-inset rounded-full transition-colors"
                  >
                    <X className="w-3 h-3 text-fg-subtle" />
                  </button>
                )}
              </div>
              <button 
                onClick={handleSearchTrigger}
                className="bg-brand text-brand-fg py-3 px-6 sm:px-4 rounded-xl hover:bg-brand-hover transition-all shadow-lg shadow-blue-100 flex items-center justify-center gap-2"
              >
                <Search className="w-4 h-4" />
                <span className="sm:hidden font-bold uppercase text-xs tracking-widest">Search</span>
              </button>
            </div>
          </div>
        </div>
      </div>

      <div className="max-w-7xl mx-auto px-4 sm:px-6 mt-6 md:mt-10">
        <div className="mb-8 overflow-x-auto pb-4 -mx-4 px-4 sm:mx-0 sm:px-0 scrollbar-hide">
          <div className="flex items-center gap-2 w-max sm:w-auto p-1.5 bg-surface border border-line rounded-2xl">
            {tabs.map((tab) => (
              <button
                key={tab}
                onClick={() => handleTabChange(tab)}
                className={`px-5 py-2.5 rounded-xl text-[10px] md:text-[11px] font-black uppercase tracking-wider transition-all whitespace-nowrap ${
                  activeTab === tab 
                  ? 'bg-brand text-white shadow-lg shadow-blue-200' 
                  : 'text-fg-subtle hover:text-fg hover:bg-surface-2'
                }`}
              >
                {tab}
              </button>
            ))}
          </div>
        </div>

        <div className="grid grid-cols-1 gap-6">
          {contractList.length > 0 ? (
            contractList.map((contract) => (
              <div key={contract.id} className="group bg-surface rounded-3xl border border-line shadow-sm hover:shadow-xl hover:border-brand-line transition-all duration-slow overflow-hidden">
                <div className="flex flex-col lg:flex-row">
                  <div className="flex-1 p-6 md:p-8 lg:border-r border-line-soft">
                    <div className="flex flex-col sm:flex-row justify-between items-start gap-4 mb-6">
                      <div className="space-y-2 max-w-full sm:max-w-[70%]">
                        <h2 className="text-lg md:text-xl font-extrabold text-fg group-hover:text-brand transition-colors line-clamp-2">{contract.project_title}</h2>
                        <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
                          <div className="flex items-center gap-1.5 shrink-0">
                            <User className="w-3.5 h-3.5 text-fg-subtle" />
                            <span className="text-[10px] text-fg-subtle font-bold uppercase tracking-tight">Contractor: <span className="text-fg">{contract.freelancer_name}</span></span>
                          </div>
                          <div className="flex items-center gap-1.5 shrink-0">
                            <Hash className="w-3.5 h-3.5 text-fg-subtle" />
                            <span className="text-[10px] text-fg-subtle font-bold uppercase tracking-tight">Ref: #ORD-{contract.id + 8000}</span>
                          </div>
                        </div>
                      </div>
                      <span className={`px-3 py-1.5 rounded-lg text-[9px] md:text-[10px] font-black uppercase tracking-widest border shrink-0 ${
                        contract.status === 'active'
                        ? 'bg-brand-soft text-brand border-brand-line' 
                        : contract.status === 'completed'
                        ? 'bg-emerald-50 text-emerald-600 border-emerald-100'
                        : 'bg-surface-2 text-fg-muted border-line-soft'
                      }`}>
                        {contract.status || 'Execution Phase'}
                      </span>
                    </div>

                    <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-6 p-5 md:p-6 bg-surface-2/50 rounded-2xl border border-line-soft">
                      <div className="space-y-1">
                        <div className="flex items-center gap-2 text-fg-subtle">
                          <Package className="w-3.5 h-3.5" />
                          <span className="text-[9px] font-black uppercase tracking-widest">Package</span>
                        </div>
                        <p className="text-xs md:text-sm font-bold text-fg uppercase truncate">{contract.package_name || 'Standard Pro'}</p>
                      </div>
                      <div className="space-y-1">
                        <div className="flex items-center gap-2 text-fg-subtle">
                          <Calendar className="w-3.5 h-3.5" />
                          <span className="text-[9px] font-black uppercase tracking-widest">Due Date</span>
                        </div>
                        <p className="text-xs md:text-sm font-bold text-fg">
                          {calculateDeadline(contract.freelancer_signed_at, contract.delivery_days)}
                        </p>
                      </div>
                      <div className="space-y-1 sm:col-span-2 md:col-span-1">
                        <div className="flex items-center gap-2 text-fg-subtle">
                          <Clock className="w-3.5 h-3.5" />
                          <span className="text-[9px] font-black uppercase tracking-widest">Delivery Time</span>
                        </div>
                        <p className="text-xs md:text-sm font-bold text-fg">{contract.delivery_days || 0} Working Days</p>
                      </div>
                    </div>
                  </div>

                  <div className="lg:w-[320px] p-6 md:p-8 bg-surface-2/40 flex flex-col justify-between gap-6">
                    <div>
                      <span className="text-[9px] font-black text-fg-subtle uppercase tracking-widest block mb-1">Total Valuation</span>
                      <div className="flex items-baseline gap-1">
                        <span className="text-2xl font-bold text-fg-subtle">$</span>
                        <span className="text-3xl font-black text-fg tracking-tighter">
                          {Number(contract.total_amount).toLocaleString()}
                        </span>
                      </div>
                    </div>

                    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-1 gap-3 w-full">
                      <button 
                        onClick={() => navigate(`/contracts/${contract.id}`)}
                        className="flex items-center justify-center gap-2 bg-brand text-brand-fg px-6 py-4 rounded-xl text-[11px] font-black uppercase tracking-widest hover:bg-brand transition-all shadow-lg shadow-blue-100/50 active:scale-95"
                      >
                        Workspace
                        <ArrowUpRight className="w-4 h-4" />
                      </button>
                      <button 
                        onClick={() => handleMessageFreelancer(contract.freelancer_id)}
                        disabled={getRoomMutation.isPending}
                        className="flex items-center justify-center gap-2 bg-surface border border-line text-fg-muted px-6 py-4 rounded-xl text-[11px] font-black uppercase tracking-widest hover:bg-surface hover:text-fg hover:border-line-strong transition-all disabled:opacity-50 active:scale-95"
                      >
                        <MessageSquare className="w-4 h-4" />
                        {getRoomMutation.isPending ? "..." : "Message"}
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            ))
          ) : (
            <EmptyState
              variant={hasActiveFilters ? "no-match" : "first-use"}
              title={hasActiveFilters ? "No contracts match your filters" : undefined}
              onClearFilters={resetFilters}
              className="bg-surface rounded-[40px] border-2 border-dashed border-line"
            />
          )}
        </div>

        {/* Replaces a hand-rolled prev/next bar whose counter read
            `contractList.length` -- this page's row count, not the range start. */}
        <Pagination
          page={page}
          pageSize={pageSize}
          count={totalCount}
          onPageChange={setPage}
          onPageSizeChange={setPageSize}
          itemNoun="contracts"
          className="mt-12 mb-10"
        />
      </div>
    </div>
  );
}