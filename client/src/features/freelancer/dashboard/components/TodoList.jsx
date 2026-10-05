import React from 'react';
import { useForm } from 'react-hook-form';
import { Trash2, Plus, Loader2 } from 'lucide-react';
import { useTodos } from '../dashboardQueries';
import { useTodoMutations } from '../dashboardMutations';
import Button from "../../../../components/common/Button";

function TodoList() {
  const { data: tasksData, isLoading } = useTodos();
  const { createTodo, deleteTodo, completeTodo, isCreating } = useTodoMutations();
  
  const tasks = tasksData?.results || [];
  const totalCount = tasksData?.count || 0;
  const isLimitReached = totalCount >= 10;

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors }
  } = useForm({
    defaultValues: {
      todo: ""
    }
  });

  const onSubmit = (data) => {
    if (!isCreating && !isLimitReached) {
      createTodo({ todo: data.todo }, {
        onSuccess: () => reset()
      });
    }
  };

  if (isLoading) return (
    <div className="bg-surface rounded-xl border border-line-soft p-6 shadow-sm h-full flex items-center justify-center">
      <Loader2 className="w-6 h-6 animate-spin text-brand" />
    </div>
  );

  return (
    <div className="bg-surface rounded-xl border border-line-soft p-6 shadow-sm h-full flex flex-col">
      <div className="flex justify-between items-center mb-6">
        <h2 className="text-[22px] font-bold text-fg leading-7" style={{ fontFamily: 'Inter, sans-serif' }}>
          Personal Tasks
        </h2>
        <span className={`text-[10px] font-bold px-2 py-1 rounded ${isLimitReached ? 'bg-danger-soft text-danger' : 'bg-brand-soft text-brand'}`}>
          {totalCount}/10 Slots
        </span>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="mb-6">
        <div className="flex gap-2">
          <div className="flex-1 relative">
            <input
              type="text"
              {...register("todo", { 
                required: "Task is required",
                maxLength: { value: 60, message: "Limit 60 characters" }
              })}
              placeholder={isLimitReached ? "Limit reached (10/10)" : "Add a new task..."}
              disabled={isLimitReached || isCreating}
              className={`w-full h-[42px] px-4 border ${errors.todo ? 'border-red-500' : 'border-line'} rounded-lg text-sm outline-none focus-visible:border-brand transition-all disabled:bg-surface-2 disabled:cursor-not-allowed`}
            />
          </div>
                    <Button
            type="submit"
            variant="primary"
            size="none"
            loading={isCreating}
            disabled={isLimitReached || isCreating}
            className="px-4 h-[42px] shadow-sm disabled:bg-surface-inset disabled:cursor-not-allowed"
          >
            <Plus className="w-4 h-4" />
            <span className="text-sm font-semibold hidden sm:inline">Add</span>
          </Button>
        </div>
        {errors.todo && <p className="text-danger-soft-fg text-[10px] mt-1 ml-1">{errors.todo.message}</p>}
      </form>

      <div className="flex flex-col gap-1 overflow-y-auto max-h-[400px] pr-2 custom-scrollbar flex-1">
        {tasks.length > 0 ? (
          tasks.map((task) => (
            <div 
              key={task.id} 
              className="group flex items-center gap-3 p-3 rounded-lg bg-transparent md:hover:bg-surface-2 border border-transparent md:hover:border-line-soft transition-all"
            >
              <input
                type="checkbox"
                checked={task.is_completed}
                onChange={() => completeTodo(task.id)}
                className="w-5 h-5 rounded accent-brand cursor-pointer flex-shrink-0"
              />
              <span className={`flex-1 text-sm transition-all break-words min-w-0 ${
                task.is_completed ? 'text-fg-subtle line-through' : 'text-fg-muted font-medium'
              }`}>
                {task.todo}
              </span>
              <button
                type="button"
                onClick={() => deleteTodo(task.id)}
                className="p-2 md:p-1.5 opacity-100 md:opacity-0 md:group-hover:opacity-100 hover:bg-danger-soft hover:text-danger-soft-fg rounded-md transition-all text-fg-subtle flex-shrink-0"
                title="Delete task"
              >
                <Trash2 className="w-4 h-4 md:w-4 md:h-4" />
              </button>
            </div>
          ))
        ) : (
          <div className="text-center py-10">
            <p className="text-fg-subtle text-sm italic">No tasks for today!</p>
          </div>
        )}
      </div>

      {isLimitReached && (
        <p className="mt-4 text-[11px] text-warning bg-warning-soft p-2 rounded border border-warning/30">
          Task limit reached. Delete a task to add a new one.
        </p>
      )}
    </div>
  );
}

export default TodoList;
