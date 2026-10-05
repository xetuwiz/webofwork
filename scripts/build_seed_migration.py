"""Generate the SeedSkillCatalog migration body from the catalogue below.

Kept as a script (not hand-written SQL) so the catalogue stays reviewable as
data. Regenerate with:

    python scripts/build_seed_migration.py

Categories use the two names already present in existing databases
('Development', 'Design') so this migration never creates a duplicate concept
alongside them; the other five use the Upwork browse-page names.
"""

CATEGORIES = [
    "Development",
    "AI & Automation",
    "Design",
    "Marketing",
    "Data & Analytics",
    "Admin & Support",
    "Writing & Translation",
]

SKILLS = {
    "Development": [
        # languages & runtimes
        "C#", "C++", "C", "Java", "Python", "JavaScript", "TypeScript", "PHP",
        "Ruby", "Rust", "Go", "Scala", "Kotlin", "Swift", "Dart", "SQL", "R",
        "Bash",
        # frontend
        "React", "React.js", "Next.js", "Vue.js", "Nuxt.js", "Angular", "Svelte",
        "Astro", "jQuery", "HTMX", "HTML5", "CSS3", "Sass", "Tailwind CSS",
        "Bootstrap", "Material UI", "Redux", "Redux Toolkit", "Zustand",
        "GraphQL", "Webpack", "Vite", "Storybook", "Jest", "Vitest", "Cypress",
        "Playwright", "Responsive Design", "Accessibility",
        # backend
        "Node.js", "Express.js", "NestJS", "Fastify", "Django", "Flask",
        "FastAPI", "Laravel", "Symfony", "Ruby on Rails", "Spring Boot",
        ".NET", ".NET Core", "ASP.NET", "ASP.NET Core", "ASP.NET MVC",
        "Entity Framework", "Dapper", "REST", "RESTful API", "gRPC",
        "WebSockets", "Microservices", "Serverless", "Event-Driven Architecture",
        "RabbitMQ", "Kafka", "Redis Pub/Sub", "Celery", "API Design",
        "OpenAPI", "Swagger", "OAuth", "OAuth 2.0", "JWT", "Authentication",
        "Authorization", "RBAC",
        # databases
        "PostgreSQL", "MySQL", "MariaDB", "SQL Server", "SQLite", "Oracle",
        "MongoDB", "DynamoDB", "Cassandra", "Firestore", "Supabase", "Redis",
        "Elasticsearch", "Neo4j", "ClickHouse", "Snowflake", "BigQuery",
        "Pinecone", "Qdrant", "pgvector", "Database Design", "Query Optimization",
        "Migrations", "Indexing", "Data Modeling", "Prisma", "TypeORM",
        "Mongoose", "Dapper", "Hibernate",
        # cloud & devops
        "AWS", "EC2", "S3", "Lambda", "RDS", "ECS", "EKS", "Fargate",
        "Google Cloud", "GCP", "GKE", "Azure", "Azure Functions", "DigitalOcean",
        "Vercel", "Netlify", "Railway", "Render", "Cloudflare", "Docker",
        "Docker Compose", "Kubernetes", "Helm", "Terraform", "Ansible",
        "Pulumi", "CloudFormation", "CI/CD", "GitHub Actions", "GitLab CI",
        "Jenkins", "ArgoCD", "Nginx", "Linux", "Bash scripting", "Monitoring",
        "Prometheus", "Grafana", "Datadog", "Sentry",
        # mobile
        "React Native", "Expo", "Flutter", "iOS", "Android", "Xamarin",
        ".NET MAUI", "Ionic", "Capacitor", "Firebase", "Push Notifications",
        "Offline-first", "App Store Optimization",
        # cms & commerce
        "WordPress", "WooCommerce", "Elementor", "Divi", "Shopify",
        "Shopify Liquid", "Webflow", "Wix", "Squarespace", "Magento",
        "Bubble.io", "Contentful", "Strapi", "Ghost", "Joomla", "Drupal",
        "Stripe", "PayPal", "Braintree",
        # qa
        "Manual Testing", "Unit Testing", "Integration Testing", "E2E Testing",
        "API Testing", "Regression Testing", "Performance Testing",
        "Security Testing", "Test Automation", "Postman", "REST Assured",
        "JMeter", "Selenium",
    ],
    "AI & Automation": [
        # models & providers
        "OpenAI", "OpenAI API", "GPT-4", "GPT-4o", "ChatGPT", "Claude",
        "Claude API", "Claude Code", "Gemini", "Llama", "Llama 3", "Mistral",
        "Gemma", "DeepSeek", "Amazon Bedrock", "Ollama", "Hugging Face",
        "Transformers", "Sentence Transformers",
        # orchestration
        "LangChain", "LangGraph", "LlamaIndex", "CrewAI", "AutoGen", "MCP",
        "Model Context Protocol", "Agentic AI", "Multi-Agent Orchestration",
        "Human-in-the-loop", "Prompt Engineering", "Prompt Architecture",
        "Few-shot Prompting", "Zero-shot Prompting", "Function Calling",
        "Tool Use", "Guardrails", "Chain-of-Thought",
        # retrieval & memory
        "RAG", "GraphRAG", "Hybrid Search", "Semantic Search", "Vector Search",
        "Embeddings", "FAISS", "ChromaDB", "Milvus",
        # ml
        "Machine Learning", "Deep Learning", "MLOps", "PyTorch", "TensorFlow",
        "Keras", "Scikit-learn", "Pandas", "NumPy", "NLP", "Computer Vision",
        "OpenCV", "Fine-tuning", "LoRA", "QLoRA", "PEFT", "RLHF", "DPO", "SFT",
        "Quantization", "GGUF", "Model Evaluation", "Model Monitoring",
        "Langfuse", "LangSmith", "Ragas",
        # speech & media
        "Whisper", "ElevenLabs", "Deepgram", "Text-to-Speech", "Speech-to-Text",
        "Voice AI", "Vapi", "Retell AI", "Twilio", "LiveKit", "Synthesia",
        "HeyGen", "AI Video Generation", "Runway", "Veo 3",
        # automation
        "n8n", "Make", "Zapier", "Airbyte", "UiPath", "Automation Anywhere",
        "Robotic Process Automation", "Web Scraping", "Scrapy", "Browser Automation",
        "Workflow Automation", "Business Process Automation", "Google Apps Script",
        "VBA", "Power Automate", "Lead Routing", "Email Automation",
        "Automation Consulting", "AI Consulting", "AI Readiness", "AI Strategy",
        "AI Governance", "AI Ethics",
    ],
    "Design": [
        # visual tools
        "Figma", "Adobe XD", "Sketch", "InVision", "Framer", "Adobe Photoshop",
        "Adobe Illustrator", "Adobe InDesign", "Adobe After Effects",
        "Adobe Premiere Pro", "Adobe Lightroom", "Adobe Firefly", "Canva",
        "CorelDRAW", "Affinity Designer", "Affinity Photo", "Procreate",
        "Blender", "Cinema 4D", "Maya", "3ds Max", "ZBrush", "Substance Painter",
        "Substance Designer", "KeyShot", "V-Ray", "Houdini", "DaVinci Resolve",
        "AutoCAD", "SketchUp",
        # design disciplines
        "Logo Design", "Brand Identity", "Brand Guidelines", "Brand Strategy",
        "Visual Identity", "Rebranding", "Typography", "Color Theory",
        "Layout Design", "Print Design", "Prepress", "Packaging Design",
        "Book Design", "Magazine Layout", "Editorial Design", "Cover Design",
        "Presentation Design", "Pitch Deck Design", "Infographics",
        "Data Visualization", "Icon Design", "Illustration", "Social Media Graphics",
        "Banner Ads", "Email Templates", "Motion Graphics", "Motion Design",
        "Text Animation", "Transitions", "VFX", "Compositing",
        # ui/ux
        "UI Design", "UX Design", "Product Design", "Interaction Design",
        "User Research", "User Flows", "User Journeys", "Personas",
        "Information Architecture", "Wireframing", "Prototyping", "Usability Testing",
        "Design Systems", "Design Tokens", "Component Libraries", "WCAG",
        "Mobile-first Design", "Conversion Rate Optimization",
        "Landing Page Design", "Dashboard Design", "SaaS Design",
        "User Experience Audit",
        # photo
        "Photo Editing", "Photo Manipulation", "Photo Retouching",
        "Product Photography", "Portrait Photography", "Commercial Photography",
        "Product Photo Editing", "E-commerce Product Images", "Background Removal",
        "Color Correction", "Old Photo Restoration",
        # video & audio
        "Video Editing", "Video Production", "Color Grading", "Sound Design",
        "Audio Mixing", "Audio Mastering", "Voiceover", "Subtitles", "Captions",
        "Explainer Video", "Podcast Editing", "Short-form Video", "UGC",
        # games & 3d
        "Unity", "Unreal Engine", "Godot", "Game Development", "Game Design",
        "Game Art", "Level Design", "3D Modeling", "3D Animation", "3D Rendering",
        "Product Visualization", "Architectural Visualization", "Texturing",
        "Rigging", "Animation", "Character Design", "Concept Art", "Blender",
        "Game Testing", "NFT Art", "Digital Collectibles",
    ],
    "Marketing": [
        "SEO", "On-Page SEO", "Off-Page SEO", "Technical SEO", "Local SEO",
        "Keyword Research", "Competitor Analysis", "Backlink Building",
        "Link Building", "Content Strategy", "Content Marketing", "Content Calendar",
        "SEM", "PPC", "Google Ads", "Facebook Ads", "Instagram Ads", "TikTok Ads",
        "LinkedIn Ads", "YouTube Ads", "Display Advertising", "Retargeting",
        "Social Media Marketing", "Social Media Management", "Community Management",
        "Influencer Marketing", "Affiliate Marketing", "Email Marketing",
        "Newsletter Marketing", "SMS Marketing", "Marketing Automation",
        "Lead Generation", "Lead Nurturing", "Sales Funnel", "Conversion Funnel",
        "Landing Page Optimization", "A/B Testing", "Marketing Analytics",
        "Google Analytics", "GA4", "Google Tag Manager", "Google Search Console",
        "HubSpot", "Mailchimp", "Klaviyo", "SendGrid", "Marketo", "Salesforce",
        "Pipedrive", "Zoho CRM", "GoHighLevel", "HubSpot", "Brand Positioning",
        "Market Research", "Customer Research", "Survey Design", "Pricing Strategy",
        "Go-to-Market Strategy", "PR", "Digital PR", "Webinar Marketing",
        "Podcast Marketing", "Video Marketing", "Content Distribution",
    ],
    "Data & Analytics": [
        "Data Analysis", "Data Visualization", "Business Intelligence", "Data Science",
        "Statistics", "A/B Testing", "Data Engineering", "Data Pipeline", "ETL",
        "Data Warehousing", "Data Modeling", "Dashboards", "Tableau", "Power BI",
        "Looker", "Metabase", "Apache Spark", "Apache Airflow", "dbt", "Snowflake",
        "BigQuery", "Redshift", "Databricks", "Apache Kafka", "Data Governance",
        "Data Quality", "Statistical Analysis", "Excel", "Advanced Excel",
        "Google Sheets", "PowerPoint",
    ],
    "Admin & Support": [
        "Virtual Assistance", "Executive Assistance", "Personal Assistance",
        "Administrative Support", "Email Management", "Calendar Management",
        "Inbox Management", "Scheduling", "Appointment Setting", "Travel Planning",
        "Data Entry", "Data Mining", "Web Research", "Internet Research",
        "List Building", "Lead Research", "CRM Data Entry", "Order Processing",
        "Customer Support", "Customer Service", "Technical Support", "Chat Support",
        "Email Support", "Phone Support", "Helpdesk", "Zendesk", "Freshdesk",
        "Intercom", "LiveChat", "Bookkeeping", "QuickBooks", "Xero", "FreshBooks",
        "Accounting", "Invoicing", "Payroll", "Financial Reporting",
        "Project Management", "Program Management", "Agile", "Scrum", "Kanban",
        "Sprint Planning", "Resource Planning", "Stakeholder Management",
        "Risk Management", "Jira", "Notion", "Airtable", "ClickUp", "Asana",
        "Trello", "Monday.com", "Basecamp", "Wrike", "Smartsheet", "Operations",
        "SOP Writing", "Process Documentation", "Recruiting", "Sourcing",
        "Screening", "Interviewing", "Onboarding", "HR", "People Ops",
        "Transcription", "Typing", "Data Annotation", "Data Labeling",
        "Content Moderation", "Microsoft Office", "Google Workspace",
    ],
    "Writing & Translation": [
        "Content Writing", "Copywriting", "SEO Writing", "Blog Writing",
        "Article Writing", "Website Copy", "Landing Page Copy", "Sales Copy",
        "Email Copy", "Ad Copy", "Social Media Copy", "Product Descriptions",
        "Technical Writing", "Documentation", "API Documentation", "User Guides",
        "White Papers", "Case Studies", "eBooks", "Ghostwriting", "Creative Writing",
        "Fiction", "Non-Fiction", "Screenwriting", "Scriptwriting", "Storyboarding",
        "Speech Writing", "Grant Writing", "Proposal Writing", "Business Plan Writing",
        "Resume Writing", "Cover Letter Writing", "LinkedIn Profile Writing",
        "Academic Writing", "Research Papers", "Scientific Writing", "Medical Writing",
        "Legal Writing", "Report Writing", "Proofreading", "Copy Editing",
        "Line Editing", "Substantive Editing", "Translation", "Localization",
        "Transcreation", "Subtitling", "Transcription", "Podcast Show Notes",
        "Newsletter Writing", "Technical Editing",
    ],
}

# Names already present in databases seeded from scripts/fixtures.sql that mean
# the same thing as a catalogue entry. The ontology resolves these as aliases at
# match time, but leaving both rows in the database means two Skill ids for one
# skill: a profile tagged 'CSharp' and a project requiring 'C#' would not join,
# and the catalogue would show both to the user. So the legacy name is folded
# into the canonical one before seeding.
#
# Direction matters: legacy -> canonical.
LEGACY_RENAMES = {
    "CSharp": "C#",
}

HEADER = '''using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebOfWork.Api.Data.Migrations
{
    /// <summary>
    /// Seeds the category and skill catalogue.
    ///
    /// Idempotent by name rather than by id: an existing database already holds
    /// 'Development', 'Design', 'CSharp', 'React', 'Python' and 'Figma' from
    /// scripts/fixtures.sql, and those rows must be reused rather than
    /// duplicated. Idempotence is what makes this safe to apply on top of a
    /// populated database as well as an empty one.
    ///
    /// Category names are the Upwork browse-page names, except that the two
    /// already in use are kept verbatim ('Development' and 'Design') so no
    /// second, synonymous row is created.
    ///
    /// Catalogue source: generated by scripts/build_seed_migration.py. Edit that
    /// script, not this file, then regenerate.
    /// </summary>
    public partial class SeedSkillCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
'''

FOOTER = '''
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Categories and skills are referenced by profile and project rows
            // that the application created. Deleting the catalogue here would
            // either cascade into user data or fail on the RESTRICT foreign
            // keys, so the seed is intentionally not reversible. To remove a
            // skill, delete it through the API once nothing references it.
        }
    }
}
'''


def sql_literal(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def main() -> None:
    out = [HEADER]
    a = out.append

    a("            // Fold legacy skill names into their canonical form first. An\n"
      "            // existing database seeded from scripts/fixtures.sql holds\n"
      "            // 'CSharp'; the catalogue calls that skill 'C#'. Renaming is\n"
      "            // preferred over inserting both, because FreelancerSkill and\n"
      "            // ProjectSkill rows point at the id -- retargeting them keeps\n"
      "            // existing profiles intact instead of orphaning their tags.\n"
      "            //\n"
      "            // The rename is skipped when the canonical name already exists,\n"
      "            // which can only happen if some row already moved across; in\n"
      "            // that case the legacy row is left alone rather than deleted,\n"
      "            // because deleting could drop tags this migration cannot see.\n")
    for legacy, canonical in LEGACY_RENAMES.items():
        a(
            "            migrationBuilder.Sql(\n"
            f"                \"UPDATE skills SET name = {sql_literal(canonical)} "
            f"WHERE name = {sql_literal(legacy)} "
            f"AND NOT EXISTS (SELECT 1 FROM skills WHERE name = "
            f"{sql_literal(canonical)} AND category_id = skills.category_id);\");\n"
        )

    a("\n            // Categories. WHERE NOT EXISTS reuses an existing row of the same\n"
      "      // name, so re-running this migration is a no-op.\n")
    for name in CATEGORIES:
        a(
            "            migrationBuilder.Sql(\n"
            f"                \"INSERT INTO categories (name) SELECT {sql_literal(name)} "
            "WHERE NOT EXISTS (SELECT 1 FROM categories WHERE name = "
            f"{sql_literal(name)});\");\n"
        )

    a("\n            // Skills. Each row resolves its category by name rather than by\n"
      "            // id: ids differ between a populated and an empty database, and\n"
      "            // the unique index is (name, category_id), so name is the identity\n"
      "            // that actually matters here.\n")
    for category, skills in SKILLS.items():
        # Preserve first-seen order while dropping duplicates: 'React' and
        # 'React.js' are distinct catalogue entries but 'Blender' appeared
        # twice across two discipline groups.
        seen = set()
        unique = []
        for skill in skills:
            if skill.lower() in seen:
                continue
            seen.add(skill.lower())
            unique.append(skill)
        a(
            f"\n            // {category} ({len(unique)} skills)\n"
        )
        for skill in unique:
            a(
                "            migrationBuilder.Sql(\n"
                f"                \"INSERT INTO skills (name, category_id, is_custom) "
                f"SELECT {sql_literal(skill)}, (SELECT id FROM categories WHERE name = "
                f"{sql_literal(category)}), false "
                "WHERE NOT EXISTS (SELECT 1 FROM skills s JOIN categories c "
                "ON c.id = s.category_id WHERE s.name = "
                f"{sql_literal(skill)} AND c.name = {sql_literal(category)});\");\n"
            )

    out.append(FOOTER)

    target = (
        "server/WebOfWork.Api/Data/migrations/"
        "20261002061656_SeedSkillCatalog.cs"
    )
    with open(target, "w", encoding="utf-8", newline="\r\n") as handle:
        handle.write("".join(out))

    total = sum(
        len({s.lower() for s in skills}) for skills in SKILLS.values()
    )
    print(f"wrote {target}")
    print(f"  {len(CATEGORIES)} categories, {total} skills")


if __name__ == "__main__":
    main()
