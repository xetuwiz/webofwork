import { useState } from "react";
import { usePendingPayouts } from "./payoutQueries.js";
import PayoutReleaseModal from "./PayoutReleaseModal.jsx";
import { useRefundPayment } from "./payoutMutations.js";
import formatMoney from "../../../utils/formatMoney.js";

export default function PaymentRelease() {
  const [page, setPage] = useState(1);
  const { data, isLoading, isError } = usePendingPayouts({ page, pageSize: 20 });
  const refund = useRefundPayment();
  const payments = data?.results || (Array.isArray(data) ? data : []);
  const [selected, setSelected] = useState(null);
  return <section className="admin-subsection"><div className="admin-subsection__heading"><div><span className="app-shell__eyebrow">Money movement</span><h1>Pending payouts</h1></div><span>{data?.count ?? payments.length} awaiting review</span></div>{isLoading && <div className="admin-loading">Loading payouts...</div>}{isError && <div className="admin-form-error" role="alert">Unable to load payouts.</div>}{!isLoading && !isError && payments.length === 0 && <div className="admin-empty">No payouts are ready for release.</div>}<div className="admin-payout-list">{payments.map((payment) => <article className="admin-payout-row" key={payment.id}><div><strong>Payment #{payment.id}</strong><small>{payment.status} · {payment.disputeStatus || "No dispute"}</small></div><strong>{formatMoney(payment.freelancerAmount ?? payment.amount)}</strong><div className="button-row"><button className="button button--accent" onClick={() => setSelected(payment)}>Release</button><button className="button button--quiet" disabled={refund.isPending} onClick={() => refund.mutate(payment.id)}>Refund</button></div></article>)}</div><div className="button-row"><button className="button button--quiet" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>Previous</button><span>Page {page}</span><button className="button button--quiet" disabled={!data?.totalPages || page >= data.totalPages} onClick={() => setPage((value) => value + 1)}>Next</button></div><PayoutReleaseModal payment={selected} onClose={() => setSelected(null)} /></section>;
}
