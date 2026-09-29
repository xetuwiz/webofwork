import axiosInstance from "../../../api/axiosInstance"

export const fetchRecommendedProjects = async()=>{
    const {data} = await axiosInstance.get('/profile/freelancer/recommended-projects/');
    return data;
}

export const fetchTodos = async () => {
    const {data} = await axiosInstance.get('/profile/todos/');
    return data;
}


export const createTodo = async (todoData) => {
    // TodosController.AddTodo takes [FromBody] string task, not an object.
    // The form supplies { todo: "..." }, so unwrap it -- posting the object
    // returned 400 "The JSON value could not be converted to System.String".
    const task = typeof todoData === "string" ? todoData : todoData?.todo;
    const { data } = await axiosInstance.post('/profile/todos/', JSON.stringify(task),
        { headers: { 'Content-Type': 'application/json' } });
    return data;
};

export const deleteTodo = async (id) => {
    const { data } = await axiosInstance.delete(`/profile/todos/${id}/`);
    return data;
};

export const completeTodo = async (id) => {
    const { data } = await axiosInstance.post(`/profile/todos/${id}/mark-complete/`);
    return data;
};