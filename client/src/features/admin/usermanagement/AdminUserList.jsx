import { useState } from "react";
import { useAdminUsers } from "./usersQueries.js";
import UserTable from "./userTable.jsx";

export default function AdminUserList() {
  const [search, setSearch] = useState("");
  const [activeSearch, setActiveSearch] = useState("");
  const [status, setStatus] = useState("");
  const { data, isLoading, isError } = useAdminUsers({ search: activeSearch || undefined, status: status || undefined });
  const users = Array.isArray(data) ? data : data?.items ?? data?.results ?? [];
  return <section className="admin-page"><div className="admin-page__heading"><div><span className="app-shell__eyebrow">Accounts</span><h1>User management</h1><p>Review access, roles, and account status without exposing private data.</p></div></div><div className="admin-filters"><input value={search} onChange={(event) => setSearch(event.target.value)} onKeyDown={(event) => event.key === "Enter" && setActiveSearch(search)} placeholder="Search name or email" aria-label="Search users" /><button className="button button--quiet" type="button" onClick={() => setActiveSearch(search)}>Search</button><select value={status} onChange={(event) => setStatus(event.target.value)} aria-label="Filter users by status"><option value="">All statuses</option><option value="Active">Active</option><option value="Blocked">Blocked</option><option value="Deleted">Deleted</option></select></div>{isLoading && <div className="admin-loading">Loading users...</div>}{isError && <div className="admin-form-error">Unable to load users.</div>}{!isLoading && !isError && <UserTable users={users} />}</section>;
}