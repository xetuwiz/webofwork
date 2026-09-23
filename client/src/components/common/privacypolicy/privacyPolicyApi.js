import axiosInstance from "../../../api/axiosInstance.js";
export const fetchPrivacyPolicy = async () => (await axiosInstance.get("/content/privacy")).data;
