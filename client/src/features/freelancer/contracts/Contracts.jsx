import React, { useState, useEffect } from 'react';
import { useMyContracts } from './contractsQueries';
import { useDebouncedValue } from '../../../hooks/useDebounce';
import Pagination from '../../../components/common/Pagination';
import EmptyState from '../../../components/common/EmptyState';
import { useListState, toQueryParams } from '../../../hooks/useListState';
import { 
  MessageSquare, 
  ExternalLink, 
  Clock, 
  Package, 
  Hash, 
  ChevronLeft, 
  ChevronRight,
  Search,
  X,
  Filter,
  Loader2
} from 'lucide-react';
import { useNavigate } from 'react-router-dom';

export default function Contracts() {
  // Tab, page, page size and the search term all live in the URL, so a specific
  // slice of the portfolio is shareable, Back steps back through tab changes
  // rather than pages, and a refresh keeps the user where they were.
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

  // Typing searches on its own after a short pause; Enter and the Search button
  // skip the wait. Previously the search only ran on Enter or click, so a user who
  // typed and then waited saw nothing at all happen.
  // Seeded from the URL so a shared link shows the term it was shared with.
  const [searchInput, setSearchInput] = useState(filters.search);
  const [searchTerm, isSearchPending] = useDebouncedValue(searchInput, 400);

  // Re-sync the field when the URL changes underneath it (Back/Forward, or a
  // reset). Render-time adjustment, which React documents for this case; an
  // effect would cost a second render pass.
  const [lastSynced, setLastSynced] = useState(filters.search);
  if (filters.search !== lastSynced) {
    setLastSynced(filters.search);
    setSearchInput(filters.search);
  }

  const navigate = useNavigate();

  const tabs = ['All', 'In Progress', 'Submitted', 'Completed', 'Disputed'];

  // Maps each tab to a real ContractStatus. 'All' -> undefined (no filter).
  // 'In Progress' -> Active, because the API only knows the enum members.
  // Declared BEFORE the hook below: reading it from the query would be a
  // temporal-dead-zone crash, which is the same bug FindTalent.jsx had.
  const STATUS_FOR_TAB = {
    All: undefined,
    'In Progress': 'Active',
    Submitted: 'Submitted',
    Completed: 'Completed',
    Disputed: 'Disputed',
  };

  const { data, isLoading } = useMyContracts(
    toQueryParams(
      {
        status: STATUS_FOR_TAB[activeTab],
        role: 'freelancer',
        search: searchTerm.trim(),
      },
      page,
      pageSize,
    ),
  );

  // Push the debounced term into the URL so it is shareable. Debounced first,
  // then written once, rather than on every keystroke -- otherwise Back would
  // step back one character at a time.
  useEffect(() => {
    if (searchTerm.trim() !== (filters.search ?? '')) {
      setFilter('search', searchTerm.trim());
    }
  }, [searchTerm, filters.search, setFilter]);

  // Enter/click commits the current input without waiting out the debounce.
  const handleSearchTrigger = () => {
    setFilter('search', searchInput.trim());
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter') {
      handleSearchTrigger();
    }
  };

  const clearSearch = () => {
    setSearchInput('');
    setFilter('search', '');
  };

  // `set` drops `page` alongside the filter, so this is the whole of
  // "changing a tab returns to page 1".
  const handleTabChange = (tab) => setFilter('tab', tab);

  if (isLoading) return (
    <div className="min-h-screen bg-surface-2 flex items-center justify-center">
      <div className="flex flex-col items-center gap-3">
        <div className="w-10 h-10 border-4 border-brand border-t-transparent rounded-full animate-spin"></div>
        <p className="text-sm font-medium text-fg-subtle">Loading your workspace...</p>
      </div>
    </div>
  );

  const contractList = data?.results || [];
  const totalCount = data?.count || 0;

  return (
    <div className="min-h-screen bg-surface-2 p-4 sm:p-8 font-sans text-fg">
      <div className="max-w-6xl mx-auto">
        
        <div className="flex flex-col md:flex-row md:items-end justify-between gap-6 mb-10">
          <div>
            <h1 className="text-3xl font-extrabold tracking-tight mb-2">My Contracts</h1>
            <p className="text-sm text-fg-subtle max-w-md leading-relaxed">
              Professional management of your freelance portfolio. Track milestones, manage deliverables, and communicate with clients.
            </p>
          </div>

          <div className="relative flex items-center gap-2">
            <div className="relative group">
              <input
                type="text"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="Search Project or ID..."
                aria-label="Search contracts"
                className="w-full sm:w-72 pl-10 pr-10 py-2.5 bg-surface border border-line rounded-lg text-sm outline-none focus-visible:ring-2 focus-visible:ring-brand/20 focus-visible:border-brand transition-all shadow-sm"
              />
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-fg-subtle group-focus-within:text-brand" />
              {isSearchPending ? (
                <Loader2
                  className="absolute right-9 top-1/2 -translate-y-1/2 h-3.5 w-3.5 animate-spin text-fg-subtle"
                  aria-hidden="true"
                />
              ) : null}
              {searchInput && (
                <button 
                  onClick={clearSearch}
                  className="absolute right-3 top-1/2 -translate-y-1/2 p-0.5 hover:bg-surface-3 rounded-full transition-colors"
                >
                  <X className="w-3.5 h-3.5 text-fg-subtle" />
                </button>
              )}
            </div>
            <button 
              onClick={handleSearchTrigger}
              className="bg-brand text-brand-fg p-2.5 rounded-lg hover:bg-brand-hover transition-all shadow-md active:scale-95"
            >
              <Search className="w-5 h-5" />
            </button>
          </div>
        </div>

        <div className="flex items-center gap-2 mb-8 overflow-x-auto pb-2 scrollbar-hide">
          <div className="flex bg-surface p-1 rounded-lg border border-line shadow-sm">
            {tabs.map((tab) => (
              <button
                key={tab}
                onClick={() => handleTabChange(tab)}
                className={`px-6 py-2 rounded-xl text-xs font-bold transition-all whitespace-nowrap ${
                  activeTab === tab 
                  ? 'bg-brand text-white shadow-sm' 
                  : 'text-fg-subtle hover:text-fg hover:bg-surface-2'
                }`}
              >
                {tab}
              </button>
            ))}
          </div>
        </div>

        <div className="space-y-4">
          {contractList.length > 0 ? (
            <>
              {contractList.map((contract) => (
                <div 
                  key={contract.id} 
                  className="group bg-surface rounded-xl border border-line-soft shadow-sm hover:shadow-xl hover:border-brand-line transition-all duration-slow overflow-hidden"
                >
                  <div className="p-6 sm:p-8">
                    <div className="flex flex-col lg:flex-row justify-between lg:items-center gap-6">
                      
                      <div className="flex-1">
                        <div className="flex items-center gap-3 mb-3">
                          <span className={`px-2.5 py-1 rounded-md text-[10px] font-black uppercase tracking-widest ${
                            contract.status === 'active' 
                            ? 'bg-brand-soft text-brand border border-brand-line' 
                            : 'bg-success-soft text-success border border-success/30'
                          }`}>
                            {contract.status === 'active' ? 'In Progress' : contract.status}
                          </span>
                          <span className="text-[10px] font-bold text-fg-subtle uppercase tracking-tighter italic">
                            Contract: #ORD-{contract.id + 8000}
                          </span>
                        </div>
                        
                        <h2 className="text-xl font-bold text-fg mb-1 group-hover:text-brand transition-colors cursor-pointer">
                          {contract.project_title}
                        </h2>
                        <p className="text-xs text-fg-subtle mb-4">
                          Client Partner: <span className="text-brand font-semibold">{contract.client_name}</span>
                        </p>
                        
                        <p className="text-sm text-fg-subtle leading-relaxed line-clamp-2 max-w-2xl">
                          {contract.project_description || "Consultancy and implementation services for the aforementioned project scope."}
                        </p>
                      </div>

                      <div className="flex flex-col sm:flex-row lg:flex-col xl:flex-row items-center gap-4 lg:min-w-[300px]">
                        <div className="grid grid-cols-2 gap-x-8 gap-y-2 w-full lg:mb-4 xl:mb-0">
                          <div className="space-y-0.5">
                            <p className="text-[10px] text-fg-subtle font-bold uppercase">Package</p>
                            <p className="text-xs font-bold text-fg-muted">{contract.package_name || 'Professional'}</p>
                          </div>
                          <div className="space-y-0.5 text-right">
                            <p className="text-[10px] text-fg-subtle font-bold uppercase">Budget</p>
                            <p className="text-sm font-black text-fg">${Number(contract.total_amount).toLocaleString()}</p>
                          </div>
                          <div className="space-y-0.5">
                            <p className="text-[10px] text-fg-subtle font-bold uppercase">Timeline</p>
                            <p className="text-xs font-bold text-fg-muted">{contract.delivery_days || 14} Days</p>
                          </div>
                        </div>

                        <div className="flex gap-2 w-full">
                          <button 
                            onClick={() => navigate(`/freelancer/contracts/${contract.id}`)}
                            className="flex-1 flex items-center justify-center gap-2 bg-brand text-brand-fg px-4 py-3 rounded-xl text-xs font-bold hover:bg-brand-hover transition-all hover:shadow-lg hover:shadow-blue-100"
                          >
                            <ExternalLink className="w-4 h-4" />
                            Details
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              ))}

              {/* Replaces a hand-rolled prev/next bar whose counter read
                  `contractList.length` -- this page's row count, not the range
                  start -- so on page 3 it said "Showing 10 of 42". */}
              <Pagination
                page={page}
                pageSize={pageSize}
                count={totalCount}
                onPageChange={setPage}
                onPageSizeChange={setPageSize}
                itemNoun="contracts"
                className="mt-8 pt-0"
              />
            </>
          ) : (
            <EmptyState
              variant={hasActiveFilters ? "no-match" : "first-use"}
              title={hasActiveFilters ? "No contracts match your filters" : undefined}
              onClearFilters={resetFilters}
              className="bg-surface rounded-3xl border-2 border-dashed border-line"
            />
          )}
        </div>
      </div>
    </div>
  );
}
