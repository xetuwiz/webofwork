import { useState } from "react";
import { usePlatformTransactions } from "./transactionQuery.js";
import formatMoney from "../../../utils/formatMoney.js";

export default function AdminTransactions() {
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("");
  const { data, isLoading, isError } = usePlatformTransactions({ page, pageSize: 20, status: status || undefined });
  const transactions = data?.results || (Array.isArray(data) ? data : []);
  return <section className="admin-subsection"><div className="admin-subsection__heading"><div><span className="app-shell__eyebrow">Platform finance</span><h1>Transactions</h1></div><span>{data?.count ?? transactions.length} payments</span></div><select className="input" value={status} onChange={(event) => { setStatus(event.target.value); setPage(1); }}><option value="">All statuses</option>{["Pending", "Escrow", "Released", "Refunded"].map((item) => <option key={item} value={item}>{item}</option>)}</select>{isLoading && <div className="admin-loading">Loading transactions...</div>}{isError && <div className="admin-form-error" role="alert">Unable to load transactions.</div>}{!isLoading && !isError && transactions.length === 0 && <div className="admin-empty">No transactions found.</div>}<div className="admin-payout-list">{transactions.map((transaction) => <article className="admin-payout-row" key={transaction.id}><div><strong>Payment #{transaction.id}</strong><small>{transaction.gateway || "PayPal"} · {transaction.status}</small></div><div><strong>{formatMoney(transaction.amount)}</strong><small>Fee {formatMoney(transaction.platformFee)}</small></div></article>)}</div><div className="button-row"><button className="button button--quiet" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}>Previous</button><span>Page {page}</span><button className="button button--quiet" disabled={!data?.totalPages || page >= data.totalPages} onClick={() => setPage((value) => value + 1)}>Next</button></div></section>;
}
