import { useQueryClient } from "@tanstack/react-query";
import { useAiWorkflows } from "../../../aiworkflow/aiWorkflowQueries.js";
import { useNotificationSocket } from "../../../../hooks/notification/useNotificationSocket.jsx";

export default function AdminHeader() {
  const client = useQueryClient();
  const { data, isLoading } = useAiWorkflows({ status: "PendingApproval", page: 1, pageSize: 1 });
  const refresh = () => client.invalidateQueries({ queryKey: ["ai-workflows"] });
  useNotificationSocket(() => refresh());
  const count = data?.count ?? data?.totalCount ?? (data?.results?.length || 0);
  return <header className="admin-header"><strong>Admin workspace</strong><a href="/admin/ai-workflows" className="admin-approval-badge">{isLoading ? "…" : count} pending</a></header>;
}
