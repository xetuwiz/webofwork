import axiosInstance from "../../../api/axiosInstance.js";

export const fetchContracts = async () => (await axiosInstance.get("/contracts")).data;
export const fetchContract = async (id) => (await axiosInstance.get(`/contracts/${id}`)).data;
export const fetchContractOffers = async () => (await axiosInstance.get("/contracts/offers")).data;
export const acceptContract = async ({ contractId, signature, signatureType = "text" }) =>
  (await axiosInstance.post(`/contracts/${contractId}/accept`, { signature, signatureType })).data;
export const requestContractRevision = async ({ contractId, reason }) =>
  (await axiosInstance.post(`/revisions/contract/${contractId}`, { reason })).data;
export const updateContractStatus = async ({ contractId, status }) =>
  (await axiosInstance.patch(`/contracts/${contractId}/status`, { status })).data;
export const verifyPayment = async (payload) => (await axiosInstance.post("/payments/verify", payload)).data;
export const createReview = async (payload) => (await axiosInstance.post("/reviews", payload)).data;
