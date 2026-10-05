# Categories & Skills API

## 1. Categories

### List Categories
GET /api/categories
**Auth:** Anonymous
Returns a list of all categories.

### Create Category
POST /api/categories/create
**Auth:** Admin
Creates a new category.

### Update Category
PATCH /api/categories/{id}
**Auth:** Admin
Updates an existing category.

### Delete Category
DELETE /api/categories/{id}
**Auth:** Admin
Deletes a category. Returns 400 if the category has associated skills or profiles.

## 2. Skills

### List Skills
GET /api/categories/skills?categoryId={id}
**Auth:** Anonymous
Returns a list of skills. Optional filter by categoryId.

### Create Skill
POST /api/categories/skills
**Auth:** Admin
Creates a new skill.

### Update Skill
PATCH /api/categories/skills/{id}
**Auth:** Admin
Updates an existing skill.

### Delete Skill
DELETE /api/categories/skills/{id}
**Auth:** Admin
Deletes a skill. Returns 400 if the skill is currently assigned to a freelancer profile.
