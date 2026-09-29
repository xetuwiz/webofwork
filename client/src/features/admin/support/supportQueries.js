import { useQuery, keepPreviousData } from "@tanstack/react-query";
import { fetchSupportTickets, fetchTicketDetails } from "./supportApi";

export const useSupportTickets = ({ page = 1, page_size = 20, status = "" } = {}) => {
  return useQuery({
    queryKey: ["supportTickets", { page, page_size, status }],
    queryFn: () => fetchSupportTickets({ page, page_size, status }),
    // `keepPreviousData` was the react-query v4 spelling, removed in v5, so it
    // was silently ignored. This is the v5 equivalent: keeps the current page on
    // screen while the next one loads instead of blanking the table.
    placeholderData: keepPreviousData,
  });
};

export const useTicketDetails = (id) => {
  return useQuery({
    queryKey: ["ticket", id],
    queryFn: () => fetchTicketDetails(id),
    enabled: !!id,
  });
};

