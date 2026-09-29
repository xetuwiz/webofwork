import { Clock, Loader2, AlertCircle, User, Briefcase } from 'lucide-react';
import { usePendingPayouts } from './payoutQueries';
import { useModal } from '../../../hooks/modal/useModalStore';
import { useRefundPayment } from './payoutMutations';
import Pagination from '../../../components/common/Pagination';
import { useListState, toQueryParams } from '../../../hooks/useListState';

export default function PaymentRelease() {
  // Page and page size live in the URL, so the view is shareable and a refresh
  // keeps the admin on the page they were reviewing.
  const { page, pageSize, setPage, setPageSize } = useListState();

  const { data: payoutData, isLoading } = usePendingPayouts(
    toQueryParams({}, page, pageSize),
  );

  // The endpoint used to return a bare array, which is why the old code read
  // `payoutData || []` and `payoutData?.length`. It now returns the standard
  // envelope (results/count/next/previous) like every other list endpoint, so
  // both arrows are actually driven by real data instead of being permanently
  // disabled against an array's undefined fields.
  const payments = payoutData?.results ?? [];
  const totalCount = payoutData?.count ?? 0;

  const { openModal } = useModal();
  const refundMutation = useRefundPayment();

  const handleRefund = (paymentId) => {
    const numericId = paymentId.replace('PAY-', '');
    if (window.confirm("Are you sure you want to refund this payment to the client?")) {
      refundMutation.mutate(numericId);
    }
  };

  if (isLoading) {
    return (
      <div className="h-screen flex items-center justify-center bg-surface-2">
        <Loader2 className="w-8 h-8 text-brand animate-spin" />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-surface-2 p-4 md:p-8">
      <div className="max-w-7xl mx-auto">
        <div className="mb-8">
          <h1 className="text-2xl md:text-3xl font-bold text-fg">Manual Payment Release</h1>
          <p className="text-fg-subtle mt-2">Review and process pending payments after the 7-day safety window.</p>
        </div>

        {payments.length === 0 ? (
          // Genuinely-empty (first-use) rather than no-match: there are no
          // filters on this page, so an empty list really does mean nothing is
          // waiting. The custom copy explains *when* payouts appear, which the
          // generic empty state cannot know.
          <div className="bg-surface rounded-2xl border border-dashed border-line-strong p-12 text-center">
            <Clock className="w-12 h-12 text-fg-subtle mx-auto mb-4" />
            <h3 className="text-lg font-semibold text-fg">No pending payouts</h3>
            <p className="text-fg-subtle">Payments will appear here 7 days after project completion.</p>
          </div>
        ) : (
          <>
            <div className="hidden lg:block bg-surface rounded-2xl shadow-sm border border-line overflow-hidden">
              <table className="w-full text-left">
                <thead className="bg-surface-2 border-b border-line">
                  <tr>
                    <th className="px-6 py-4 text-xs font-semibold text-fg-subtle uppercase tracking-wider">Payment ID</th>
                    <th className="px-6 py-4 text-xs font-semibold text-fg-subtle uppercase tracking-wider">Client / Freelancer</th>
                    <th className="px-6 py-4 text-xs font-semibold text-fg-subtle uppercase tracking-wider">Amount</th>
                    <th className="px-6 py-4 text-xs font-semibold text-fg-subtle uppercase tracking-wider">Dispute Info</th>
                    <th className="px-6 py-4 text-xs font-semibold text-fg-subtle uppercase tracking-wider">Status</th>
                    <th className="px-6 py-4 text-xs font-semibold text-fg-subtle uppercase tracking-wider text-center">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {payments.map((pay) => {
                    const hasDispute = !!pay.dispute_status;
                    const isRefundingThis = refundMutation.isPending && refundMutation.variables === pay.payment_id.replace('PAY-', '');
                    
                    const shouldShowRelease = !hasDispute || 
                      (pay.dispute_raised_by === 'freelancer' && pay.dispute_status === 'resolved') || 
                      (pay.dispute_raised_by === 'client' && pay.dispute_status === 'rejected');

                    const shouldShowRefund = hasDispute && (
                      (pay.dispute_raised_by === 'client' && pay.dispute_status === 'resolved') || 
                      (pay.dispute_raised_by === 'freelancer' && pay.dispute_status === 'rejected')
                    );

                    return (
                      <tr key={pay.contract_id} className="hover:bg-surface-2/50 transition-colors">
                        <td className="px-6 py-5">
                          <span className="font-medium text-fg-muted block">{pay.payment_id}</span>
                          <span className="text-[10px] text-fg-subtle uppercase font-bold">{pay.gateway}</span>
                        </td>
                        <td className="px-6 py-5">
                          <div className="text-sm text-wrap max-w-[200px] break-words">
                            <p className="text-fg font-semibold">{pay.client_name} <span className="text-fg-subtle font-normal text-xs">(C)</span></p>
                            <p className="text-fg-subtle">{pay.freelancer_name} <span className="text-fg-subtle font-normal text-xs">(F)</span></p>
                          </div>
                        </td>
                        <td className="px-6 py-5 font-bold text-fg">${Number(pay.freelancer_share).toLocaleString()}</td>
                        <td className="px-6 py-5">
                          {hasDispute ? (
                            <div className="flex flex-col gap-1 break-words max-w-[150px]">
                              <span className={`inline-flex items-center gap-1 text-[10px] font-bold uppercase px-2 py-0.5 rounded w-fit ${pay.dispute_status === 'resolved' ? 'bg-emerald-50 text-emerald-700' : 'bg-rose-50 text-rose-700'}`}>
                                <AlertCircle className="w-3 h-3 shrink-0" /> {pay.dispute_status}
                              </span>
                              <span className="text-[10px] text-fg-subtle flex items-center gap-1">
                                {pay.dispute_raised_by === 'client' ? <User className="w-3 h-3 shrink-0" /> : <Briefcase className="w-3 h-3 shrink-0" />}
                                Raised by {pay.dispute_raised_by}
                              </span>
                            </div>
                          ) : (
                            <span className="text-xs text-fg-subtle italic">No Dispute</span>
                          )}
                        </td>
                        <td className="px-6 py-5">
                          <span className="inline-flex items-center px-3 py-1 rounded-full text-xs font-medium bg-warning-soft text-warning border border-warning/30 capitalize">
                            {pay.status}
                          </span>
                        </td>
                        <td className="px-6 py-5">
                          <div className="flex items-center justify-center gap-3">
                            {shouldShowRelease && (
                              <button 
                                onClick={() => openModal('release-payout', { id: pay.contract_id, total_amount: Number(pay.freelancer_share), platform_fee: Number(pay.platform_fee), refunded_amount: Number(pay.refunded_amount || 0) })}
                                className="bg-brand hover:bg-brand-hover text-brand-fg px-5 py-2 rounded-lg text-sm font-semibold transition-all shadow-sm active:scale-95 whitespace-nowrap"
                              >
                                Release
                              </button>
                            )}
                            {shouldShowRefund && (
                              <button 
                                disabled={refundMutation.isPending}
                                onClick={() => handleRefund(pay.payment_id)}
                                className="text-rose-600 border border-rose-200 hover:bg-rose-50 px-5 py-2 rounded-lg text-sm font-semibold disabled:opacity-50 whitespace-nowrap min-w-[100px]"
                              >
                                {isRefundingThis ? 'Processing...' : 'Refund'}
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>

            <div className="lg:hidden space-y-4">
              {payments.map((pay) => {
                const hasDispute = !!pay.dispute_status;
                const isRefundingThis = refundMutation.isPending && refundMutation.variables === pay.payment_id.replace('PAY-', '');
                
                const shouldShowRelease = !hasDispute || 
                      (pay.dispute_raised_by === 'freelancer' && pay.dispute_status === 'resolved') || 
                      (pay.dispute_raised_by === 'client' && pay.dispute_status === 'rejected');

                const shouldShowRefund = hasDispute && (
                      (pay.dispute_raised_by === 'client' && pay.dispute_status === 'resolved') || 
                      (pay.dispute_raised_by === 'freelancer' && pay.dispute_status === 'rejected')
                    );

                return (
                  <div key={pay.contract_id} className="bg-surface p-5 rounded-2xl border border-line shadow-sm break-words overflow-hidden">
                    <div className="flex justify-between items-start mb-4 gap-2">
                      <div className="min-w-0">
                        <span className="text-xs font-bold text-brand uppercase block truncate">{pay.payment_id}</span>
                        <h3 className="font-bold text-fg text-lg break-all">${Number(pay.freelancer_share).toLocaleString()}</h3>
                      </div>
                      <div className="flex flex-col items-end gap-2 shrink-0">
                        <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-warning-soft text-warning border border-warning/30">
                          {pay.status}
                        </span>
                        {hasDispute && (
                           <span className="text-[9px] font-black uppercase text-rose-600 text-right">Disputed By {pay.dispute_raised_by}</span>
                        )}
                      </div>
                    </div>
                    <div className="grid grid-cols-2 gap-4 mb-6">
                      <div className="min-w-0">
                        <p className="text-xs text-fg-subtle uppercase font-bold tracking-tighter">Client</p>
                        <p className="text-sm text-fg-muted font-medium break-words">{pay.client_name}</p>
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs text-fg-subtle uppercase font-bold tracking-tighter">Freelancer</p>
                        <p className="text-sm text-fg-muted font-medium break-words">{pay.freelancer_name}</p>
                      </div>
                    </div>
                    <div className="flex flex-col sm:flex-row gap-3">
                      {shouldShowRelease && (
                        <button 
                          onClick={() => openModal('release-payout', { id: pay.contract_id, total_amount: Number(pay.freelancer_share), platform_fee: Number(pay.platform_fee), refunded_amount: Number(pay.refunded_amount || 0) })}
                          className="flex-1 bg-brand text-brand-fg py-3 rounded-xl font-bold text-sm transition-all active:scale-95"
                        >
                          Release Payment
                        </button>
                      )}
                      {shouldShowRefund && (
                        <button 
                          disabled={refundMutation.isPending}
                          onClick={() => handleRefund(pay.payment_id)}
                          className={`flex-1 text-rose-600 border border-rose-200 hover:bg-rose-50 py-3 rounded-xl font-bold text-sm disabled:opacity-50 transition-all`}
                        >
                          {isRefundingThis ? 'Processing...' : 'Refund Client'}
                        </button>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>

            {/* Replaces a hand-rolled prev/next bar. The old one read
                `payments.length` -- this page's row count, not the range start --
                so on page 3 it said "Showing 10 of 42". More importantly both
                arrows were disabled outright, because the endpoint returned an
                array and `array.next` is always undefined. */}
            <Pagination
              page={page}
              pageSize={pageSize}
              count={totalCount}
              onPageChange={setPage}
              onPageSizeChange={setPageSize}
              itemNoun="payouts"
              className="mt-8 pt-0"
            />
          </>
        )}
      </div>
    </div>
  );
}