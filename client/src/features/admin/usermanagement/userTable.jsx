import { useState } from "react";
import { useSoftDeleteUser, useToggleUserActive } from "./usersMutations.js";
import ViewUserModal from "./components/ViewUserModal.jsx";
import SuspendUserModal from "./components/SuspendUserModal.jsx";
import DeleteUserModal from "./components/DeleteUserModal.jsx";

export default function UserTable({ users }) {
  const [selected, setSelected] = useState(null);
  const [modal, setModal] = useState(null);
  const toggle = useToggleUserActive();
  const remove = useSoftDeleteUser();

  return <><div className="admin-table-wrap"><table className="admin-table"><thead><tr><th>User</th><th>Role</th><th>Status</th><th>Joined</th><th>Actions</th></tr></thead><tbody>{users.map((user) => <tr key={user.id}><td><button className="admin-table__user" type="button" onClick={() => { setSelected(user); setModal("view"); }}><span className="admin-user-avatar">{user.fullName?.slice(0, 1).toUpperCase()}</span><span><strong>{user.fullName}</strong><small>{user.email}</small></span></button></td><td>{user.role}</td><td><span className={user.isActive ? "admin-status admin-status--active" : "admin-status"}>{user.status}</span></td><td>{new Date(user.createdAt).toLocaleDateString()}</td><td>{String(user.status).toLowerCase() === "deleted" ? <span className="admin-archived">Archived</span> : <div className="admin-table__actions"><button type="button" onClick={() => { setSelected(user); setModal("suspend"); }}>{user.isActive ? "Block" : "Activate"}</button><button type="button" onClick={() => { setSelected(user); setModal("delete"); }}>Archive</button></div>}</td></tr>)}</tbody></table>{users.length === 0 && <div className="admin-empty">No users match the current filters.</div>}</div><ViewUserModal user={modal === "view" ? selected : null} onClose={() => setModal(null)} /><SuspendUserModal user={modal === "suspend" ? selected : null} onClose={() => setModal(null)} pending={toggle.isPending} error={toggle.error} onConfirm={() => toggle.mutate(selected.id, { onSuccess: () => setModal(null) })} /><DeleteUserModal user={modal === "delete" ? selected : null} onClose={() => setModal(null)} pending={remove.isPending} error={remove.error} onConfirm={() => remove.mutate(selected.id, { onSuccess: () => setModal(null) })} /></>;
}