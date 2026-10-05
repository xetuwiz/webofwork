"""Canonical example inputs, used by both the fixture recorder and the tests.

## Why these live here

A fixture is keyed on ``sha256`` of the prompt *exactly as the chain sends it*.
So the recording step and the replay step have to compose that prompt identically
-- and when each side built its own copy, they drifted, and the test failed with
"no recorded response". That is the fixture provider working exactly as designed:
it detected that a prompt no longer matched its recording.

One definition removes the whole class of problem. Change a prompt and re-record
here; the recorder and the tests follow automatically.

The values are chosen to exercise the interesting paths: a project with two
required skills, a fixed price, a category that exists, and a pool of two
candidates who differ on skill, rating and price.
"""

from __future__ import annotations

#: The categories the AI service is given with a draft request. Ids are the real
#: ones from the seeded development database.
#:
#: The skills are NESTED, and that is load-bearing rather than decorative. These
#: rows used to be bare `{id, name}` -- copied from the payload the .NET side
#: actually sent, which projected only `Id` and `Name`. Grounding then had no way
#: to tell a category row from a flat skill row (both are `{id, name}`), so it
#: promoted all four categories into the skill pool and "validated" category
#: names as skills.
#:
#: The consequence was not cosmetic. A test could assert `skill_ids == [1]` for a
#: skill named "React" when the only row in the catalogue was the category
#: `{"id": 1, "name": "Web Development"}` -- so it passed by proving that a
#: category id is a valid skill id. In production the same path returned
#: `skills: ["Development", "Design"]` as the drafter's entire skill selection,
#: out of654 real skills it had never been shown.
#:
#: Nesting here keeps these fixtures honest about the shape the server sends. A
#: test that wants to exercise a category-only payload should say so explicitly,
#: via `CATEGORIES_WITHOUT_SKILLS`, rather than leaving every test ambiguous.
CATEGORIES: list[dict] = [
    {
        "id": 1,
        "name": "Web Development",
        "skills": [
            {"id": 1, "name": "C#"},
            {"id": 2, "name": "React"},
            {"id": 3, "name": "Python"},
            {"id": 21, "name": "HTML5"},
            {"id": 22, "name": "CSS3"},
        ],
    },
    {
        "id": 2,
        "name": "Mobile Development",
        "skills": [
            {"id": 4, "name": "Flutter"},
            {"id": 5, "name": "Swift"},
            {"id": 6, "name": "Kotlin"},
        ],
    },
    {
        "id": 3,
        "name": "Design",
        "skills": [
            {"id": 7, "name": "Figma"},
            {"id": 8, "name": "Adobe Photoshop"},
            {"id": 9, "name": "Illustrator"},
        ],
    },
    {
        "id": 4,
        "name": "Writing & Translation",
        "skills": [
            {"id": 10, "name": "Copywriting"},
            {"id": 11, "name": "Technical Writing"},
            {"id": 12, "name": "Translation"},
        ],
    },
]

#: The shape the server used to send: categories with no skills at all.
#:
#: Kept because the drafter has to survive it -- an older or misconfigured server
#: will send exactly this, and the correct response is "no skills matched, pick
#: some in the form", never "the category names are the skills". Used by the
#: regression test that pins that behaviour.
CATEGORIES_WITHOUT_SKILLS: list[dict] = [
    {"id": c["id"], "name": c["name"]} for c in CATEGORIES
]

#: A project projection in the shape ``/internal/read/projects/{id}`` returns
#: *after* normalisation -- camelCase keys, real numbers.
PROJECT: dict = {
    "id": 1,
    "title": "Customer portal with Stripe billing",
    "description": "A logged-in portal where customers see invoices and pay by card.",
    "requirements": "Stripe checkout, invoice history, email receipts.",
    "expectedDeliverables": "Deployed app, admin view, test suite.",
    "pricingType": "Fixed",
    "fixedPrice": 2500,
    "minPrice": None,
    "maxPrice": None,
    "expectedDays": 30,
    "status": "Open",
    "clientId": "c0000000-0000-0000-0000-000000000001",
    "categoryId": 1,
    "skills": ["React", "CSharp"],
}

#: The platform settings the planner and validation agents read.
THRESHOLDS: dict = {
    "minBriefConfidence": 0.6,
    "proposalScreeningThreshold": 0.5,
    "matchScoreThreshold": 0.6,
}

COMMISSION: dict = {"commissionPercentage": 10, "currency": "USD"}

#: Two candidates, deliberately different on every axis the scorer looks at, so a
#: shortlist is ordered rather than a tie.
FREELANCERS: list[dict] = [
    {
        "id": "b944584e-2cd9-43f9-b5c7-a25e2c865ff4",
        "fullName": "Sam",
        "tagline": "Full-stack developer",
        "availability": True,
        "averageRating": 5.0,
        "reviewCount": 12,
        "skills": ["React"],
        "packages": [
            {"id": "p1", "packageType": "pro", "priceUsd": 2200.0,
             "deliveryDays": 21, "description": "Pro"}
        ],
    },
    {
        "id": "c2b7f0e1-1111-2222-3333-444455556666",
        "fullName": "Alex",
        "tagline": "Backend specialist",
        "availability": True,
        "averageRating": 4.2,
        "reviewCount": 3,
        "skills": ["CSharp"],
        "packages": [
            {"id": "p2", "packageType": "pro", "priceUsd": 2800.0,
             "deliveryDays": 30, "description": "Pro"}
        ],
    },
]

#: What a client has actually typed when they ask for help drafting. Chosen to be
#: thin enough that grounding has something to do.
DRAFT_PARTIAL_TEXT = (
    "We need a customer portal where people can pay invoices by card. Stripe. "
    "3 weeks."
)
