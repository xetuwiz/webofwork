import { useQuery } from "@tanstack/react-query";
import { fetchSupportTickets, fetchTicketDetails } from "./supportApi.js";
export const useSupportTickets = (params = {}) => useQuery({ queryKey: ["admin-support", params], queryFn: () => fetchSupportTickets(params) });
export const useTicketDetails = (id) => useQuery({ queryKey: ["admin-ticket", id], queryFn: () => fetchTicketDetails(id), enabled: Boolean(id) });
