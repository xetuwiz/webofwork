import {useMutation, useQueryClient } from "@tanstack/react-query";
import { updateTicketReply } from "./supportApi";
import { toastError, toastSuccess } from "../../../utils/toast";

export const useReplyMutation = () => {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: updateTicketReply,
    onSuccess: (data) => {
      queryClient.invalidateQueries(["supportTickets"]);
      queryClient.invalidateQueries(["ticket", data.id]);
      toastSuccess("Reply sent and email triggered successfully!");
    },
    onError: (error) => {
      toastError(error.response?.data?.message || "Failed to send reply");
    },
  });
};