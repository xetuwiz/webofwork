-- Phase 18: validate-row-counts.sql
-- Validate that all expected tables contain data after import.
-- Run from the repository root: psql -d webofwork -f scripts/validate-row-counts.sql

\set ON_ERROR_STOP on
\echo 'Validating imported row counts...'

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
    v bigint;
BEGIN
    FOREACH t IN ARRAY tables LOOP
        IF to_regclass('public.' || t) IS NULL THEN
            RAISE WARNING 'Missing table: %', t;
            CONTINUE;
        END IF;

        EXECUTE format('SELECT count(*) FROM public.%I', t) INTO v;
        RAISE NOTICE '% => % rows', t, v;

        IF v = 0 THEN
            RAISE WARNING 'Table % has 0 rows after import.', t;
        END IF;
    END LOOP;
END $$;

\echo 'Row-count validation complete.'
