import axiosInstance from "../../../api/axiosInstance"

export const fetchFreelancerProfile = async ()=>{
    const res = await axiosInstance.get('/profile/freelancer/profile/');
    return res.data;
}

export const updateProfile = async (data) => {
    // Was PATCHing '/profile/freelancer/profile/', which only has a GET
    // route, so every save returned 405 Method Not Allowed and the UI
    // reported "Failed to update profile". The write endpoint is
    // PATCH /api/profile/freelancer/me (FreelancersController).
    const res = await axiosInstance.patch('/profile/freelancer/me', data);
    return res.data;
}

export const fetchFreelancerTransactions = async ({ status, range, page }) => {
  const { data } = await axiosInstance.get("/payments/freelancer/transaction-history/", {
    params: {
      status: status !== "All Statuses" ? status.toLowerCase() : undefined,
      range: range !== "All Time" ? range : undefined,
      page: page,
    },
  });
  return data;
};

export const fetchPaymentSettings = async () => {
    const { data } = await axiosInstance.get('/profile/freelancer/payment-settings/');
    return data;
};

export const updatePaymentSettings = async (payload) => {
    const { data } = await axiosInstance.put('/profile/freelancer/payment-settings/', payload);
    return data;
};

// Portfolio items persisted through dedicated item-level endpoints. These are
// deliberately NOT part of the profile PATCH: UpdateFreelancerProfileRequest has
// no Portfolio field, so PATCHing them into /profile/freelancer/me was silently
// dropped and the "Save Changes" button claimed to save work it never touched.
export const createPortfolioItem = async (data) => {
    const res = await axiosInstance.post('/profile/freelancer/portfolio/', data);
    return res.data;
};

export const updatePortfolioItem = async (id, data) => {
    const res = await axiosInstance.patch(`/profile/freelancer/portfolio/${id}/`, data);
    return res.data;
};

export const deletePortfolioItem = async (id) => {
    await axiosInstance.delete(`/profile/freelancer/portfolio/${id}/`);
};