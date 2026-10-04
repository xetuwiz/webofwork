// Verifies ProjectListModel against a payload captured from the live
// WebOfWork API (GET /api/projects/freelancer/project-list/).
//
// This is the contract mismatch that matters: the template expected Laravel's
// { jobs: { data: [...] } } envelope, ours returns a flat DRF-style page. If
// this regresses, the job list silently renders zero rows.
import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:webofwork/models/project_list_model.dart';

void main() {
  const captured = r"""{
 "results": [
  {
   "id": 9,
   "title": "Build a Task Management Web Application 2",
   "description": "I need a full-stack task management web application where users can create accounts, create and manage tasks, set priorities and deadlines, and track their progress. The application should have a clean, responsive interface and a simple dashboard.",
   "category": 1,
   "category_name": "Development",
   "pricing_type": "fixed",
   "fixed_price": "2000.00",
   "min_budget": null,
   "max_budget": null,
   "delivery_days": 14,
   "status": "open",
   "created_at": "2026-09-27T13:54:29.228Z",
   "skills": [
    "CSharp",
    "Python",
    "React"
   ]
  },
  {
   "id": 7,
   "title": "Workflow test range project",
   "description": "A description long enough to pass validation rules.",
   "category": 1,
   "category_name": "Development",
   "pricing_type": "range",
   "fixed_price": null,
   "min_budget": "500.00",
   "max_budget": "900.00",
   "delivery_days": 10,
   "status": "open",
   "created_at": "2026-09-27T09:51:28.271Z",
   "skills": [
    "CSharp",
    "React",
    "Python"
   ]
  },
  {
   "id": 6,
   "title": "Workflow test fixed project",
   "description": "A description long enough to pass validation rules.",
   "category": 1,
   "category_name": "Development",
   "pricing_type": "fixed",
   "fixed_price": "1234.50",
   "min_budget": null,
   "max_budget": null,
   "delivery_days": 10,
   "status": "open",
   "created_at": "2026-09-27T09:51:26.181Z",
   "skills": [
    "CSharp",
    "React",
    "Python"
   ]
  },
  {
   "id": 2,
   "title": "Figma landing page mockups",
   "description": "Seeded project Figma landing page mockups. Full description for workflow testing.",
   "category": 1,
   "category_name": "Development",
   "pricing_type": "fixed",
   "fixed_price": "350.00",
   "min_budget": null,
   "max_budget": null,
   "delivery_days": 7,
   "status": "open",
   "created_at": "2026-09-27T09:50:13.604Z",
   "skills": [
    "CSharp",
    "React"
   ]
  }
 ],
 "count": 4,
 "page": 1,
 "page_size": 20,
 "total_pages": 1
}""";

  test('parses the WebOfWork page envelope', () {
    final model = ProjectListModel.fromJson(jsonDecode(captured) as Map);
    final projects = model.projects;
    expect(projects, isNotNull);
    expect(projects!.projects, isNotNull);
    expect(projects.projects!.length, greaterThan(0));
    expect(projects.total, 4);
  });

  test('maps pricing, delivery and skills onto the card fields', () {
    final model = ProjectListModel.fromJson(jsonDecode(captured) as Map);
    final project = model.projects!.projects!.first;
    expect(project.title, isNotEmpty);
    expect(project.budget, isNotNull, reason: 'fixed_price must reach .budget');
    expect(project.duration, contains('days'));
    expect(project.projectSkills, isNotEmpty,
        reason: 'skills list must be populated');
    expect(project.projectSkills.first.skill, isNotEmpty);
    expect(project.createdAt, isNotNull,
        reason: 'created_at is "MM/dd/yyyy HH:mm:ss", not ISO-8601');
  });
}
