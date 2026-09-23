import { useMutation, useQueryClient } from "@tanstack/react-query";
import { refundPayment, releasePayout } from "./payoutApi.js";
export const useReleasePayout = () => { const client = useQueryClient(); return useMutation({ mutationFn: releasePayout, onSuccess: () => { client.invalidateQueries({ queryKey: ["pending-payouts"] }); client.invalidateQueries({ queryKey: ["dashboard-stats"] }); } }); };
export const useRefundPayment = () => { const client = useQueryClient(); return useMutation({ mutationFn: refundPayment, onSuccess: () => { client.invalidateQueries({ queryKey: ["pending-payouts"] }); client.invalidateQueries({ queryKey: ["admin-transactions"] }); } }); };
