"""
Does the drafter ever see the whole category?

## The failure this exists for

A live run of `list_skills_by_category(1, search="web")` returned exactly three
rows -- Webflow, Webpack, WebSockets. The drafter returned those three, because
`MIN_REQUIRED_SKILLS` is three and three was all it had been shown. The proposal
was a build toolchain for a marketing website.

HTML5, CSS3, JavaScript and Responsive Design are all in that category. They were
never in front of the model. The model behaved correctly given its inputs; the
tool had misdescribed the world.

The cause was formatting. 199 skills serialised as JSON is 12,443 characters and
`MAX_TOOL_RESULT_CHARS` is 4,000, so the result was cut -- alphabetically, which
drops the middle of the alphabet. `search` existed to get past the cut, so the
model used it, with the one word its brief supplied.
"""

import json
import re

import pytest

from ai.agents.draft_agent import MIN_REQUIRED_SKILLS, compact_reference_rows
from ai.agents.tool_loop import MAX_TOOL_RESULT_CHARS


def _row(rid, name, category_id=1, is_custom=False):
    """A skill row exactly as the .NET boundary sends it."""
    return {
        "id": rid,
        "name": name,
        "category_id": category_id,
        "is_custom": is_custom,
    }


# The real Development catalogue, in the order the endpoint returns it.
# 199 rows, alphabetical -- the shape that caused the cut.
DEVELOPMENT = [
    "Accessibility", "Android", "Angular", "Ansible", "API Design", "API Testing",
    "App Store Optimization", "ArgoCD", "ASP.NET", "ASP.NET Core", "ASP.NET MVC",
    "Astro", "Authentication", "Authorization", "AWS", "Azure", "Azure Functions",
    "Bash", "Bash scripting", "BigQuery", "Bootstrap", "Braintree", "Bubble.io",
    "C", "C#", "C++", "Capacitor", "Cassandra", "Celery", "CI/CD", "ClickHouse",
    "Cloudflare", "CloudFormation", "Contentful", "CSS3", "Cypress", "Dapper",
    "Dart", "Database Design", "Datadog", "Data Modeling", "DigitalOcean", "Divi",
    "Django", "Docker", "Docker Compose", "Drupal", "DynamoDB", "E2E Testing",
    "EC2", "ECS", "EKS", "Elasticsearch", "Elementor", "Entity Framework",
    "Event-Driven Architecture", "Expo", "Express.js", "Fargate", "FastAPI",
    "Fastify", "Firebase", "Firestore", "Flask", "Flutter", "GCP", "Ghost",
    "GitHub Actions", "GitLab CI", "GKE", "Go", "Google Cloud", "Grafana",
    "GraphQL", "gRPC", "Helm", "Hibernate", "HTML5", "HTMX", "Indexing",
    "Integration Testing", "Ionic", "iOS", "Java", "JavaScript", "Jenkins",
    "Jest", "JMeter", "Joomla", "jQuery", "JWT", "Kafka", "Kotlin", "Kubernetes",
    "Lambda", "Laravel", "Linux", "Magento", "Manual Testing", "MariaDB",
    "Material UI", "Microservices", "Migrations", "MongoDB", "Mongoose",
    "Monitoring", "MySQL", "Neo4j", "NestJS", ".NET", ".NET Core", "Netlify",
    ".NET MAUI", "Next.js", "Nginx", "Node.js", "Nuxt.js", "OAuth", "OAuth 2.0",
    "Offline-first", "OpenAPI", "Oracle", "PayPal", "Performance Testing",
    "pgvector", "PHP", "Pinecone", "Playwright", "PostgreSQL", "Postman",
    "Prisma", "Prometheus", "Pulumi", "Push Notifications", "Python", "Qdrant",
    "Query Optimization", "R", "RabbitMQ", "Railway", "RBAC", "RDS", "React",
    "React.js", "React Native", "Redis", "Redis Pub/Sub", "Redux",
    "Redux Toolkit", "Regression Testing", "Render", "Responsive Design", "REST",
    "REST Assured", "RESTful API", "Ruby", "Ruby on Rails", "Rust", "S3", "Sass",
    "Scala", "Security Testing", "Selenium", "Sentry", "Serverless", "Shopify",
    "Shopify Liquid", "Snowflake", "Spring Boot", "SQL", "SQLite", "SQL Server",
    "Squarespace", "Storybook", "Strapi", "Stripe", "Supabase", "Svelte",
    "Swagger", "Swift", "Symfony", "Tailwind CSS", "Terraform",
    "Test Automation", "TypeORM", "TypeScript", "Unit Testing", "Vercel", "Vite",
    "Vitest", "Vue.js", "Webflow", "Webpack", "WebSockets", "Wix", "WooCommerce",
    "WordPress", "Xamarin", "Zustand",
]

DEV_ROWS = [_row(i + 1, n) for i, n in enumerate(DEVELOPMENT)]


class TestTheCatalogueIsNotTruncated:
    def test_the_real_json_exceeds_the_budget(self):
        """The premise. If this stops being true the bug cannot recur this way."""
        as_json = json.dumps(DEV_ROWS, separators=(",", ":"))

        assert len(as_json) > MAX_TOOL_RESULT_CHARS

    def test_the_compact_form_fits_the_whole_category(self):
        compact = compact_reference_rows(DEV_ROWS)

        assert len(compact) <= MAX_TOOL_RESULT_CHARS

    def test_nothing_is_announced_as_cut_when_it_all_fit(self):
        compact = compact_reference_rows(DEV_ROWS)

        assert "TRUNCATED" not in compact

    def test_every_skill_survives_into_the_text(self):
        compact = compact_reference_rows(DEV_ROWS)

        missing = [n for n in DEVELOPMENT if n not in compact]

        assert missing == []

    @pytest.mark.parametrize(
        "name",
        ["HTML5", "CSS3", "JavaScript", "Responsive Design", "TypeScript", "React"],
    )
    def test_the_skills_a_website_brief_needs_are_present(self, name):
        """The four the live run could not see, plus two more it would need."""
        compact = compact_reference_rows(DEV_ROWS)

        assert name in compact


class TestTheSearchFootgun:
    def test_a_website_brief_would_only_ever_have_shown_these(self):
        """Why `search=web` was fatal, kept as a test so the reason is not lost.

        This is not a test of our code; it is a test of the catalogue's shape and
        the endpoint's substring match. It documents that "website" -> "web"
        cannot reach CSS3, and that the only defence is not searching.
        """
        matched = [n for n in DEVELOPMENT if "web" in n.lower()]

        assert matched == ["Webflow", "Webpack", "WebSockets"]
        assert len(matched) < MIN_REQUIRED_SKILLS + 1
        assert "CSS3" not in matched

    def test_the_three_web_matches_are_not_a_website_build(self):
        """What the model was actually asked to choose between."""
        matched = {n for n in DEVELOPMENT if "web" in n.lower()}

        # A no-code builder, a bundler, and a realtime transport. None of these is
        # what a client asking for a responsive site is hiring for.
        assert matched == {"Webflow", "Webpack", "WebSockets"}


class TestFormat:
    def test_ids_are_kept_so_the_model_can_quote_them(self):
        compact = compact_reference_rows([_row(175, "Webflow"), _row(40, "Webpack")])

        assert "175 Webflow" in compact
        assert "40 Webpack" in compact

    def test_accepts_the_pascal_case_spelling(self):
        # The .NET boundary serialises with capitalised keys; the agent contracts
        # are snake_case. Both spellings are real, so both are read.
        compact = compact_reference_rows(
            [{"Id": 7, "Name": "Django"}, {"id": 8, "name": "Flask"}]
        )

        assert "7 Django" in compact
        assert "8 Flask" in compact

    def test_empty_input_says_so_rather_than_returning_nothing(self):
        assert compact_reference_rows([]) == "No rows."

    def test_rows_without_a_name_are_skipped_not_rendered_as_none(self):
        compact = compact_reference_rows([{"id": 1}, {"id": 2, "name": ""}, _row(3, "React")])

        assert compact == "3 React"

    def test_survives_junk_entries(self):
        compact = compact_reference_rows([None, "a string", 42, _row(9, "Go")])

        assert compact == "9 Go"

    def test_a_row_with_no_id_still_renders_its_name(self):
        assert compact_reference_rows([{"name": "Custom Skill"}]) == "Custom Skill"


class TestHonestyWhenItReallyIsTooBig:
    """The compact format is not a licence to hide a cut."""

    def _huge(self):
        return [_row(i + 1, f"SkillNumber{i:05d}") for i in range(1000)]

    def test_an_oversized_list_is_announced(self):
        compact = compact_reference_rows(self._huge())

        assert "TRUNCATED" in compact

    def test_the_notice_says_how_many_were_cut(self):
        # The exact count depends on how long the names are, so the invariant is
        # that the arithmetic is self-consistent -- not that it equals some
        # number I worked out by hand.
        compact = compact_reference_rows(self._huge())

        omitted = int(re.search(r"Omitted: (\d+)", compact).group(1))
        kept = len(re.findall(r"\d+ SkillNumber\d{5}", compact))

        assert omitted == 1000 - kept
        assert 0 < omitted < 1000

    def test_the_notice_reports_a_cut_rather_than_a_silent_one(self):
        compact = compact_reference_rows(self._huge())

        assert "further skill(s) were cut" in compact

    def test_the_notice_does_not_pretend_they_are_absent(self):
        # An alphabetical cut removes the middle of the alphabet, so a model told
        # only "here is the list" concludes the missing names do not exist.
        compact = compact_reference_rows(self._huge())

        assert "not absent from the platform" in compact

    def test_the_visible_part_still_fits_the_budget(self):
        compact = compact_reference_rows(self._huge())

        assert len(compact) <= MAX_TOOL_RESULT_CHARS + 200
