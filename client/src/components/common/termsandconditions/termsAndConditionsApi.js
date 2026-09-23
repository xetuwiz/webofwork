import axiosInstance from "../../../api/axiosInstance.js";
export const fetchTermsAndConditions = async () => (await axiosInstance.get("/content/terms")).data;
