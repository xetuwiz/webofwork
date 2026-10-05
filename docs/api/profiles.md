# Profile API

## Freelancer Endpoints
- `GET /api/freelancers/me`: Gets the authenticated freelancer's profile.
- `PATCH /api/freelancers/me`: Updates the freelancer profile.
- `POST /api/freelancers/onboard`: Bulk inserts onboarding info (skills, packages).
- `POST /api/freelancers/skills`: Adds a skill.
- `DELETE /api/freelancers/skills/{id}`: Deletes a skill.
- `POST /api/freelancers/packages`: Adds a package.
- `PATCH /api/freelancers/packages/{id}`: Updates a package.
- `DELETE /api/freelancers/packages/{id}`: Deletes a package.
- `GET /api/freelancers/payment-settings`: Gets payment settings.
- `PUT /api/freelancers/payment-settings`: Updates payment settings.

## Client Endpoints
- `GET /api/profiles/client/me`: Gets the client profile.
- `PATCH /api/profiles/client/me`: Updates the client profile.

## Common
- `POST /api/profiles/switch-role`: Switches active role between client and freelancer.
