import { useModal } from "../../../hooks/modal/useModalStore";
import { formatDateDMY } from "../../../utils/date";
import { useSoftDeleteUser, useToggleUserActive } from "./usersMutations";
import { MoreVertical, Ban, Unlock, Trash2, ExternalLink } from "lucide-react";
import Pagination from "../../../components/common/Pagination";

export default function UserTable({
  users,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange,
  totalCount,
}) {
  const toggleMutation = useToggleUserActive();
  const deleteMutation = useSoftDeleteUser();
  const { openModal } = useModal();

  const getRoleStyle = (role) => {
    switch (role?.toLowerCase()) {
      case "freelancer": return "bg-warning-soft text-warning-soft-fg border-warning/30";
      case "client": return "bg-brand-soft text-brand border-brand-line";
      case "both": return "bg-surface-3 text-fg-muted border-indigo-100";
      default: return "bg-surface-2 text-fg-muted border-line-soft";
    }
  };

  const handleViewProfile = (u) => {
    openModal("view-user", { user: u });
  };

  const handleDelete = (u) => {
    openModal("delete-user", {
      userName: u.full_name,
      userEmail: u.email,
      onConfirm: () => deleteMutation.mutate(u.id)
    });
  };

  const handleSuspendClick = (u) => {
    openModal("suspend-user", {
      userName: u.full_name,
      userEmail: u.email,
      isActive: u.is_active,
      onConfirm: () => toggleMutation.mutate(u.id)
    });
  };

  return (
    <div className="bg-surface border border-line rounded-xl shadow-sm overflow-hidden">
      <table className="w-full text-sm text-left border-collapse">
        <thead className="bg-surface-2/50 border-b border-line">
          <tr>
            <th className="px-6 py-4 font-semibold text-fg">User</th>
            <th className="px-6 py-4 font-semibold text-fg">Status</th>
            <th className="px-6 py-4 font-semibold text-fg">Role</th>
            <th className="px-4 py-4 font-semibold text-fg">Joined Date</th>
            <th className="px-6 py-4 font-semibold text-fg text-right">Actions</th>
          </tr>
        </thead>

        <tbody className="divide-y divide-gray-100">
          {users.map((u) => (
            <tr key={u.id} className="hover:bg-surface-2/50 transition-colors group">
              <td className="px-6 py-4">
                <div 
                  className="flex items-center gap-3 cursor-pointer group/item"
                  onClick={() => handleViewProfile(u)}
                >
                  <div className="h-10 w-10 rounded-full overflow-hidden bg-brand flex items-center justify-center text-brand-fg font-bold text-xs shadow-sm ring-2 ring-transparent group-hover/item:ring-brand/20 transition-all">
                    {u.profile_photo ? (
                      <img className="w-full h-full object-cover" src={u.profile_photo} alt="" referrerPolicy="no-referrer" />
                    ) : (
                      u.full_name?.charAt(0)
                    )}
                  </div>
                  <div>
                    <p className="font-semibold text-fg leading-none mb-1 group-hover/item:text-brand transition-colors flex items-center gap-1">
                      {u.full_name}
                      <ExternalLink size={12} className="opacity-0 group-hover/item:opacity-100" />
                    </p>
                    <p className="text-xs text-fg-subtle">{u.email}</p>
                  </div>
                </div>
              </td>

              <td className="px-3 py-4">
                {u.is_deleted ? (
                  <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-danger-soft text-danger border border-danger/30">
                    Deleted
                  </span>
                ) : u.is_active ? (
                  <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-success-soft text-success border border-success/30">
                    <span className="w-1.5 h-1.5 rounded-full bg-success-soft mr-1.5"></span>
                    Active
                  </span>
                ) : (
                  <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-warning-soft text-warning border border-warning/30">
                    Blocked
                  </span>
                )}
              </td>

              <td className="px-3 py-4">
                <span className={`px-2.5 py-0.5 rounded-lg text-[10px] font-bold uppercase tracking-wider border ${getRoleStyle(u.is_freelancer ? 'both' : 'client')}`}>
                  {u.is_freelancer ? "Both" : u.current_role || "Client"}
                </span>
              </td>

              <td className="px-4 py-4 text-fg-muted">
                {formatDateDMY(u.created_at)}
              </td>

              <td className="px-6 py-4">
                {!u.is_deleted ? (
                  <div className="flex justify-end gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                    <button
                      onClick={() => handleSuspendClick(u)}
                      className={`p-2 rounded-lg border transition-all ${
                        u.is_active 
                        ? "text-warning hover:bg-warning-soft border-line" 
                        : "text-success hover:bg-success-soft border-line"
                      }`}
                    >
                      {u.is_active ? <Ban size={16} /> : <Unlock size={16} />}
                    </button>
                    <button
                      onClick={() => handleDelete(u)}
                      className="p-2 text-danger hover:bg-danger-soft border border-line rounded-lg transition-all"
                    >
                      <Trash2 size={16} />
                    </button>
                  </div>
                ) : (
                   <div className="text-right">
                     <span className="text-[10px] font-bold uppercase text-fg-subtle bg-surface-3 px-2 py-1 rounded">Archived</span>
                   </div>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>

        {/* Replaces a hand-rolled prev/next bar. The old one read
            `users.length` -- this page's row count, not the range start -- so on
            page 3 of 10 it said "Showing 10 of 42". The shared component derives
            the real range from page / pageSize / count, and adds the page-size
            selector and numbered pages. */}
        <div className="px-6 py-4 bg-surface-2/50 border-t border-line">
          <Pagination
            page={page}
            pageSize={pageSize}
            count={totalCount}
            onPageChange={onPageChange}
            onPageSizeChange={onPageSizeChange}
            itemNoun="users"
            className="pt-0"
          />
        </div>
    </div>
  );
}