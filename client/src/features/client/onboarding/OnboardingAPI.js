import axiosInstance from "../../../api/axiosInstance.js";

export const sendPhoneOtp = async ({ phone }) => (await axiosInstance.post("/freelancers/phone/send-otp", { phone })).data;
export const verifyPhoneOtp = async ({ phone, otp }) => (await axiosInstance.post("/freelancers/phone/verify-otp", { phone, otp })).data;
export const fetchFreelancerProfile = async () => (await axiosInstance.get("/profile/freelancer/me")).data;
export const submitFreelancerOnboarding = async (payload) => (await axiosInstance.post("/freelancers/onboard", payload)).data;
export const createPortfolioItem = async (payload) => (await axiosInstance.post("/profile/freelancer/portfolio", payload)).data;
export const updatePaymentSettings = async ({ paypalEmail }) => (await axiosInstance.put("/freelancers/payment-settings", JSON.stringify(paypalEmail), { headers: { "Content-Type": "application/json" } })).data;
