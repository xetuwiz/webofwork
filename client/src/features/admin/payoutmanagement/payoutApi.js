import axiosInstance from "../../../api/axiosInstance";

export const fetchPendingPayouts = async ({ page = 1, page_size = 20 } = {}) => {
  // Was `?page=${page}` by string concatenation, with no page size at all.
  // AdminParityController.PendingPayouts ignored `page` entirely and returned a
  // bare array; it now returns the standard paged envelope and honours
  // `page_size` (it accepts both spellings).
  const response = await axiosInstance.get("/admin/pending-payouts/", {
    params: { page, page_size },
  });
  return response.data;
};

export const releasePayout = async (contractId) => {
  const response = await axiosInstance.post("/payments/release-payout/", {
    contract_id: contractId,
  });
  return response.data;
};
export const refundPayment = async (paymentId) => {
  const { data } = await axiosInstance.post("/payments/refund-payment/", {
    payment_id: paymentId,
  });
  return data;
};
