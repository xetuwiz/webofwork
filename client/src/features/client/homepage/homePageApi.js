import axiosInstance from "../../../api/axiosInstance.js";

export const switchRole = async ({ role }) => (await axiosInstance.post("/profile/switch-role", JSON.stringify(String(role).toLowerCase()), { headers: { "Content-Type": "application/json" } })).data;
export const fetchRecommendedFreelancers = async () => (await axiosInstance.get("/profile/recommended-freelancers")).data;
