import axiosInstance from "../../../api/axiosInstance.js";

export const getMyProfile = async () => {
  const response = await axiosInstance.get("/profile/me");
  return response.data;
};

export const updateProfile = async (payload) => {
  const response = await axiosInstance.patch("/profile/me", payload);
  return response.data;
};

export const changePassword = async (payload) => {
  const response = await axiosInstance.post("/auth/change-password", payload);
  return response.data;
};

export const fetchClientSpendingSummary = async () => {
  const response = await axiosInstance.get("/payments/client/spending");
  return response.data;
};

export const fetchClientReviews = async (userId) => {
  const response = await axiosInstance.get("/reviews/me/client", {
    params: userId ? { userId } : undefined,
  });
  return response.data;
};
