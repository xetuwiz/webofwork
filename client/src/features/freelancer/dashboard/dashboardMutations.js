import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toastApiError, toastSuccess } from "../../../utils/toast";
import { createTodo, deleteTodo, completeTodo} from './DashboardApi'

export const useTodoMutations = () => {
    const queryClient = useQueryClient();

    const createMutation = useMutation({
        mutationFn: createTodo,
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['todos'] });
            toastSuccess("Task added successfully");
        },
        onError: (error) => toastApiError(error, "Failed to add task")
    });

    // These two previously discarded the error entirely -- `onError: () => ...`
    // with no parameter -- so a rejected delete or a 401 on a stale session both
    // reported the same generic line with no cause. Accepting the error costs
    // nothing and is the difference between "Could not delete task" and the
    // server's reason.
    const deleteMutation = useMutation({
        mutationFn: deleteTodo,
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['todos'] });
            toastSuccess("Task deleted");
        },
        onError: (error) => toastApiError(error, "Could not delete task")
    });

    const completeMutation = useMutation({
        mutationFn: completeTodo,
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['todos'] });
        },
        onError: (error) => toastApiError(error, "Failed to update status")
    });

    return {
        createTodo: createMutation.mutate,
        isCreating: createMutation.isPending,
        deleteTodo: deleteMutation.mutate,
        isDeleting: deleteMutation.isPending,
        completeTodo: completeMutation.mutate,
        isCompleting: completeMutation.isPending
    };
};