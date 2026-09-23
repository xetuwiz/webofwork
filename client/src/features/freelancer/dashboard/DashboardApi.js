import axiosInstance from "../../../api/axiosInstance.js";

export const fetchRecommendedProjects = async () => (await axiosInstance.get("/freelancers/recommended-projects")).data;
export const fetchTodos = async () => (await axiosInstance.get("/todos")).data;
export const createTodo = async (payload) => (await axiosInstance.post("/todos", JSON.stringify(typeof payload === "string" ? payload : payload.task), { headers: { "Content-Type": "application/json" } })).data;
export const deleteTodo = async (id) => (await axiosInstance.delete(`/todos/${id}`)).data;
export const completeTodo = async (id) => (await axiosInstance.patch(`/todos/${id}/toggle`)).data;
