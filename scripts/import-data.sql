-- Phase 18: import-data.sql
-- Import a snapshot back into the database from ./data_exports.
-- Run from the repository root: psql -d webofwork -f scripts/import-data.sql

\set ON_ERROR_STOP on
\echo 'Importing WebOfWork data snapshot...'

DO $$
DECLARE
    tables text[] := ARRAY[
        'validation_results',
        'agent_recommendations',
        'tool_call_logs',
        'workflow_step_logs',
        'workflow_runs',
        'global_settings',
        'faqs',
        'cms_sections',
        'support_tickets',
        'notifications',
        'messages',
        'chat_rooms',
        'platform_analytics',
        'payments',
        'dispute_files',
        'disputes',
        'contract_revisions',
        'contract_deliverables',
        'milestone_deliverables',
        'milestones',
        'contracts',
        'invitations',
        'proposal_screenings',
        'proposals',
        'project_skills',
        'projects',
        'skills',
        'categories',
        'reviews',
        'brief_analyses',
        'brief_attachments',
        'briefs',
        'freelancer_todos',
        'freelancer_payment_settings',
        'skill_endorsements',
        'portfolio_tags',
        'portfolio_media',
        'freelancer_portfolios',
        'freelancer_packages',
        'freelancer_skills',
        'freelancer_profiles',
        'client_profiles',
        'users'
    ];
    t text;
BEGIN
    FOREACH t IN ARRAY tables LOOP
        IF to_regclass('public.' || t) IS NOT NULL THEN
            EXECUTE format('TRUNCATE public.%I RESTART IDENTITY CASCADE', t);
            RAISE NOTICE 'Truncated %', t;
        END IF;
    END LOOP;
END $$;

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
                'COPY public.%I FROM %L WITH (FORMAT csv, HEADER true)',
                t,
                './data_exports/' || t || '.csv'
            );
            RAISE NOTICE 'Imported %', t;
        ELSE
            RAISE NOTICE 'Skipped missing table %', t;
        END IF;
    END LOOP;
END $$;

\echo 'Import complete.'
