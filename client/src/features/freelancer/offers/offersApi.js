import axiosInstance from "../../../api/axiosInstance.js";
export const fetchMyOffers = async () => (await axiosInstance.get("/contracts/offers")).data;
export const acceptContract = async ({ contractId, data }) => (await axiosInstance.post(`/contracts/${contractId}/accept`, data)).data;
