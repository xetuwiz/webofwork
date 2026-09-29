import axiosInstance from "../../../api/axiosInstance";

export const fetchFreelancerReviews = async (freelancerId, { page = 1, page_size = 20 } = {}) => {
  // The endpoint returned a bare array and ignored `page`, while this page read
  // `data.results` / `data.count` / `data.next` -- so the review list rendered
  // nothing at all. It now returns the standard paged envelope.
  const response = await axiosInstance.get(`/profile/freelancers/${freelancerId}/reviews/`, {
    params: { page, page_size },
  });

  return response.data;
};
