import axiosInstance from "../../../api/axiosInstance.js";
export const fetchJobDetails = async (id) => (await axiosInstance.get(`/projects/${id}`)).data;
