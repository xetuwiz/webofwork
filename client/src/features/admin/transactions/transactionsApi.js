import axiosInstance from '../../../api/axiosInstance'
export const fetchTransactions = async ({ page = 1, page_size = 20, status = "" } = {}) => {
    // `page_size` to match the rest of the API; PaymentsController accepts both
    // this and `pageSize`.
    const response = await axiosInstance.get('/payments/platform-transactions/', {
        params: { page, page_size, status }
    })
    return response.data;
}