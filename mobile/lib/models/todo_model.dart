class TodoItem {
  final String id;
  final String todo;
  final bool isCompleted;

  TodoItem({
    required this.id,
    required this.todo,
    required this.isCompleted,
  });

  factory TodoItem.fromJson(Map<String, dynamic> json) {
    return TodoItem(
      id: json['id'] ?? '',
      todo: json['todo'] ?? '',
      isCompleted: json['isCompleted'] ?? false,
    );
  }
}
