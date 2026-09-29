# Portfolio Media & Skill Taxonomy API

## 1. Portfolio Media

### Upload Media

POST /api/freelancers/portfolio/media (Multipart Form)
**Auth:** Freelancer
Uploads an image, video, or document (PDF) to a Portfolio item.

- Requires portfolioItemId (Guid)
- Requires ile (max 10MB)

### Delete Media

DELETE /api/freelancers/portfolio/media/{mediaId}
**Auth:** Freelancer
Deletes media from the portfolio (and Cloudinary).

### Reorder Media

PATCH /api/freelancers/portfolio/{portfolioItemId}/media/reorder
**Auth:** Freelancer
**Body:** [ "guid1", "guid2" ]
Updates orderIndex to match the given list.
