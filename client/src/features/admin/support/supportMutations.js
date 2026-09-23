import { useMutation, useQueryClient } from "@tanstack/react-query";
import { updateTicketReply } from "./supportApi.js";
export const useReplyMutation = () => { const client = useQueryClient(); return useMutation({ mutationFn: updateTicketReply, onSuccess: () => { client.invalidateQueries({ queryKey: ["admin-support"] }); client.invalidateQueries({ queryKey: ["admin-ticket"] }); } }); };
