import { useQuery } from "@tanstack/react-query";
import axiosInstance from "../../api/axiosInstance";

/**
 * The dispute-reason vocabulary, from the server.
 *
 * Both dispute modals used to hardcode their own `<option>` list. The freelancer
 * one offered 4 of the 8 reasons, the client one offered a different 4, and their
 * labels were written from each side's point of view ("Client requesting extra
 * work outside scope" vs "Freelancer is unresponsive"). GET /disputes/reasons is the
 * single source, same rationale as /notifications/preferences/types.
 */
export const fetchDisputeReasons = async () => {
  const { data } = await axiosInstance.get("/disputes/reasons/");
  return data?.results ?? [];
};

export const useDisputeReasons = () =>
  useQuery({
    queryKey: ["dispute-reasons"],
    queryFn: fetchDisputeReasons,
    // The vocabulary is effectively static; a dispute is rare, so there is no
    // reason to re-fetch it.
    staleTime: Infinity,
  });

/**
 * Reason labels are written for the person raising the dispute, so "no feedback"
 * means the counterparty went quiet. Prefix by the raiser's role to keep that
 * meaning without the server having to know who is asking.
 */
export const reasonLabel = (option, raisedBy) => {
  if (!option) return "";
  const base = option.label;
  if (raisedBy !== "client") {
    return base === "No feedback" ? "Client is unresponsive" : base;
  }
  return base === "No feedback" ? "Freelancer is unresponsive" : base;
};
