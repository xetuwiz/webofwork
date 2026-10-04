import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:webofwork/helper/app_urls.dart';
import 'package:webofwork/models/todo_model.dart';
import 'package:webofwork/data/network/network_api_services.dart';

class TodoService with ChangeNotifier {
  List<TodoItem> _todos = [];
  List<TodoItem> get todos => _todos;

  bool _isLoading = false;
  bool get isLoading => _isLoading;

  final NetworkApiServices _apiService = NetworkApiServices();

  Future<void> fetchTodos() async {
    _isLoading = true;
    notifyListeners();

    try {
      final response = await _apiService.getApi(AppUrls.todosUrl, 'Fetch Todos');
      if (response != null && response is List) {
        _todos =
            response.map((e) => TodoItem.fromJson(Map<String, dynamic>.from(e))).toList();
      }
    } catch (e) {
      debugPrint("Error fetching todos: $e");
    } finally {
      _isLoading = false;
      notifyListeners();
    }
  }

  Future<void> addTodo(String task) async {
    try {
      // AddTodo takes `[FromBody] string task`, so the body must be a bare JSON
      // string ("buy milk"), not an object -- an object returns HTTP 400
      // "The JSON value could not be converted to System.String".
      final response =
          await _apiService.postApi(jsonEncode(task), AppUrls.todosUrl, 'Add Todo');
      if (response is Map && response['id'] != null) {
        _todos.add(TodoItem.fromJson(Map<String, dynamic>.from(response)));
        notifyListeners();
      }
    } catch (e) {
      debugPrint("Error adding todo: $e");
    }
  }

  /// Marks a todo complete. The backend endpoint is one-way (it always sets
  /// `IsCompleted = true`), so this must NOT flip local state -- doing so made
  /// the checkbox appear to un-check and then snap back on the next fetch.
  Future<void> completeTodo(String id) async {
    final index = _todos.indexWhere((t) => t.id == id);
    if (index == -1) return;
    if (_todos[index].isCompleted) return;

    _todos[index] = TodoItem(
      id: id,
      todo: _todos[index].todo,
      isCompleted: true,
    );
    notifyListeners();

    try {
      await _apiService
          .postApi({}, '${AppUrls.todosUrl}/$id/mark-complete', 'Complete Todo');
    } catch (e) {
      // Revert on fail
      _todos[index] = TodoItem(
        id: id,
        todo: _todos[index].todo,
        isCompleted: false,
      );
      notifyListeners();
      debugPrint("Error completing todo: $e");
    }
  }

  Future<void> deleteTodo(String id) async {
    final index = _todos.indexWhere((t) => t.id == id);
    if (index == -1) return;

    final removed = _todos.removeAt(index);
    notifyListeners();

    try {
      await _apiService.deleteApi('${AppUrls.todosUrl}/$id', 'Delete Todo');
    } catch (e) {
      // Revert on fail
      _todos.insert(index, removed);
      notifyListeners();
      debugPrint("Error deleting todo: $e");
    }
  }
}
