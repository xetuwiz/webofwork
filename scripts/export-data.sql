-- Phase 18: export-data.sql
-- Export a snapshot of the current application data into CSV files in ./data_exports.
-- Run from the repository root: psql -d webofwork -f scripts/export-data.sql

\set ON_ERROR_STOP on
\echo 'Exporting WebOfWork data snapshot...'
\! mkdir -p ./data_exports 2>/dev/null || mkdir data_exports 2>nul

DO $$
DECLARE
    tables text[] := ARRAY[
        'users',
        'client_profiles',
        'freelancer_profiles',
        'freelancer_skills',
        'freelancer_packages',
        'freelancer_portfolios',
        'portfolio_media',
        'portfolio_tags',
        'skill_endorsements',
        'freelancer_payment_settings',
        'freelancer_todos',
        'briefs',
        'brief_attachments',
        'brief_analyses',
        'reviews',
        'categories',
        'skills',
        'projects',
        'project_skills',
        'proposals',
        'proposal_screenings',
        'invitations',
        'contracts',
        'milestones',
        'milestone_deliverables',
        'contract_deliverables',
        'contract_revisions',
        'disputes',
        'dispute_files',
        'payments',
        'platform_analytics',
        'chat_rooms',
        'messages',
        'notifications',
        'support_tickets',
        'cms_sections',
        'faqs',
        'global_settings',
        'workflow_runs',
        'workflow_step_logs',
        'tool_call_logs',
        'agent_recommendations',
        'validation_results'
    ];
    t text;
BEGIN
    FOREACH t IN ARRAY tables LOOP
        IF to_regclass('public.' || t) IS NOT NULL THEN
            EXECUTE format(
                'COPY public.%I TO %L WITH (FORMAT csv, HEADER true)',
                t,
                './data_exports/' || t || '.csv'
            );
            RAISE NOTICE 'Exported %', t;
        ELSE
            RAISE NOTICE 'Skipped missing table %', t;
        END IF;
    END LOOP;
END $$;

\echo 'Export complete. Snapshot files are in ./data_exports.'
