import axiosInstance from "../../../api/axiosInstance.js";
export const fetchGlobalSettings = async () => (await axiosInstance.get("/admin/settings/global")).data;
export const updateGlobalSettings = async (payload) => (await axiosInstance.patch("/admin/settings/global", payload)).data;
