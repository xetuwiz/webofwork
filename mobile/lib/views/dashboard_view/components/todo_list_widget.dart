import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/services/todo_service.dart';

class TodoListWidget extends StatefulWidget {
  const TodoListWidget({super.key});

  @override
  State<TodoListWidget> createState() => _TodoListWidgetState();
}

class _TodoListWidgetState extends State<TodoListWidget> {
  final TextEditingController _controller = TextEditingController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<TodoService>().fetchTodos();
    });
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  String _slotLabel(int count) => count == 1 ? 'Slot' : 'Slots';

  @override
  Widget build(BuildContext context) {
    return Consumer<TodoService>(
      builder: (context, ts, child) {
        // The web client caps personal tasks at 10 (`totalCount >= 10` disables
        // the input and shows "N/10 Slots"). The server caps at 400, but the
        // app matches the web so the limits are coherent.
        final isLimitReached = ts.todos.length >= 10;
        return Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: context.dProvider.whiteColor,
            borderRadius: BorderRadius.circular(8),
            border: Border.all(color: context.dProvider.black8),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Add task input
              Row(
                children: [
                  Expanded(
                    child: TextField(
                      controller: _controller,
                      maxLength: 60,
                      enabled: !isLimitReached,
                      decoration: InputDecoration(
                        hintText: isLimitReached
                            ? 'Limit reached (10/10)'
                            : 'Add a new task...',
                        counterText: '',
                        isDense: true,
                        contentPadding:
                            const EdgeInsets.symmetric(horizontal: 12, vertical: 12),
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(8),
                          borderSide: BorderSide(color: context.dProvider.black7),
                        ),
                      ),
                      onSubmitted: (val) {
                        if (isLimitReached) return;
                        if (val.trim().isNotEmpty) {
                          ts.addTodo(val.trim());
                          _controller.clear();
                        }
                      },
                    ),
                  ),
                  const SizedBox(width: 8),
                  IconButton(
                    icon: const Icon(Icons.add_circle),
                    color: context.dProvider.primaryColor,
                    onPressed: isLimitReached
                        ? null
                        : () {
                            if (_controller.text.trim().isNotEmpty) {
                              ts.addTodo(_controller.text.trim());
                              _controller.clear();
                            }
                          },
                  )
                ],
              ),
              const SizedBox(height: 4),
              Row(
                children: [
                  Text(
                    '${ts.todos.length}/10 ${_slotLabel(ts.todos.length)}',
                    style: context.titleSmall?.copyWith(
                      color: isLimitReached
                          ? context.dProvider.warningColor
                          : context.dProvider.primaryColor,
                    ),
                  ),
                  const Spacer(),
                  if (isLimitReached)
                    Text(
                      'Limit reached. Delete a task to add a new one.',
                      style: context.bodySmall?.copyWith(
                        color: context.dProvider.warningColor,
                      ),
                    ),
                ],
              ),
              const SizedBox(height: 16),

              if (ts.isLoading && ts.todos.isEmpty)
                const Center(child: CircularProgressIndicator())
              else if (ts.todos.isEmpty)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 24),
                  child: Center(
                    child: Text(
                      'All caught up!',
                      style:
                          context.titleSmall?.copyWith(color: context.dProvider.black5),
                    ),
                  ),
                )
              else
                ListView.separated(
                  shrinkWrap: true,
                  physics: const NeverScrollableScrollPhysics(),
                  itemCount: ts.todos.length,
                  separatorBuilder: (context, index) => const Divider(height: 1),
                  itemBuilder: (context, index) {
                    final todo = ts.todos[index];
                    return ListTile(
                      contentPadding: EdgeInsets.zero,
                      leading: Checkbox(
                        value: todo.isCompleted,
                        // The backend only supports "mark complete", so a
                        // finished task is locked rather than reversible.
                        onChanged:
                            todo.isCompleted ? null : (val) => ts.completeTodo(todo.id),
                      ),
                      title: Text(
                        todo.todo,
                        style: context.titleSmall?.copyWith(
                          decoration:
                              todo.isCompleted ? TextDecoration.lineThrough : null,
                          color: todo.isCompleted
                              ? context.dProvider.black5
                              : context.dProvider.black3,
                        ),
                      ),
                      trailing: IconButton(
                        icon: const Icon(Icons.delete_outline, size: 20),
                        color: context.dProvider.warningColor,
                        onPressed: () => ts.deleteTodo(todo.id),
                      ),
                    );
                  },
                ),
            ],
          ),
        );
      },
    );
  }
}
