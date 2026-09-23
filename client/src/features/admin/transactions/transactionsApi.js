import axiosInstance from "../../../api/axiosInstance.js";
export const fetchTransactions = async (params = {}) => (await axiosInstance.get("/payments/admin/transactions", { params })).data;
