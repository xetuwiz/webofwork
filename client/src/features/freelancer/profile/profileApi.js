import axiosInstance from "../../../api/axiosInstance.js";
export const fetchFreelancerProfile = async () => (await axiosInstance.get("/profile/freelancer/me")).data;
export const updateProfile = async (payload) => (await axiosInstance.patch("/profile/freelancer/me", payload)).data;
export const fetchFreelancerTransactions = async (params = {}) => (await axiosInstance.get("/payments/freelancer/history", { params })).data;
export const fetchPaymentSettings = async () => (await axiosInstance.get("/freelancers/payment-settings")).data;
export const updatePaymentSettings = async (payload) => {
	const paypalEmail = typeof payload === "string" ? payload : payload.paypalEmail;
	return (await axiosInstance.put("/freelancers/payment-settings", JSON.stringify(paypalEmail), { headers: { "Content-Type": "application/json" } })).data;
};
