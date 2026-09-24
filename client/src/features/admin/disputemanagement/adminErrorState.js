export function getAdminErrorMessage(error) {
  if (error?.response?.data?.detail) return error.response.data.detail;
  if (error?.response?.data?.error) return error.response.data.error;
  if (error?.code === "ERR_NETWORK" || error?.message === "Network Error") return "Unable to reach the server. Check your connection.";
  return "Something went wrong. Please try again.";
}
