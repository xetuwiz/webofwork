import axiosInstance from "../../../api/axiosInstance.js";
export const fetchPendingPayouts = async (params = {}) => (await axiosInstance.get("/payouts/pending", { params })).data;
export const releasePayout = async (paymentId) => (await axiosInstance.post("/payouts/release", { paymentId })).data;
export const refundPayment = async (paymentId) => (await axiosInstance.post("/refunds", { paymentId })).data;
