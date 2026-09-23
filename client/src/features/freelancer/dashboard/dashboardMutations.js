import { useMutation, useQueryClient } from "@tanstack/react-query";
import { completeTodo, createTodo, deleteTodo } from "./DashboardApi.js";
export const useTodoMutations = () => {
  const client = useQueryClient();
  const refresh = () => client.invalidateQueries({ queryKey: ["todos"] });
  return { createTodo: useMutation({ mutationFn: createTodo, onSuccess: refresh }), deleteTodo: useMutation({ mutationFn: deleteTodo, onSuccess: refresh }), completeTodo: useMutation({ mutationFn: completeTodo, onSuccess: refresh }) };
};
export const useCreateTodo = () => { const client = useQueryClient(); return useMutation({ mutationFn: createTodo, onSuccess: () => client.invalidateQueries({ queryKey: ["todos"] }) }); };
export const useDeleteTodo = () => { const client = useQueryClient(); return useMutation({ mutationFn: deleteTodo, onSuccess: () => client.invalidateQueries({ queryKey: ["todos"] }) }); };
export const useCompleteTodo = () => { const client = useQueryClient(); return useMutation({ mutationFn: completeTodo, onSuccess: () => client.invalidateQueries({ queryKey: ["todos"] }) }); };
