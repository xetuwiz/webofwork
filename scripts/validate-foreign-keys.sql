-- Phase 18: validate-foreign-keys.sql
-- Ensure imported data does not contain orphan rows against core foreign-key relationships.
-- Run from the repository root: psql -d webofwork -f scripts/validate-foreign-keys.sql

\set ON_ERROR_STOP on
\echo 'Validating foreign-key integrity...'

DO $$
DECLARE
    v bigint;
BEGIN
    IF to_regclass('public.project_skills') IS NOT NULL AND to_regclass('public.skills') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.project_skills ps
        LEFT JOIN public.skills s ON s.id = ps.skill_id
        WHERE ps.skill_id IS NOT NULL AND s.id IS NULL;
        RAISE NOTICE 'project_skills.skill_id orphan rows: %', v;
        IF v > 0 THEN RAISE WARNING 'project_skills contains orphan skill references'; END IF;
    END IF;

    IF to_regclass('public.briefs') IS NOT NULL AND to_regclass('public.client_profiles') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.briefs b
        LEFT JOIN public.client_profiles cp ON cp.id = b.client_id
        WHERE b.client_id IS NOT NULL AND cp.id IS NULL;
        RAISE NOTICE 'briefs.client_id orphan rows: %', v;
        IF v > 0 THEN RAISE WARNING 'briefs contains orphan client references'; END IF;
    END IF;

    IF to_regclass('public.projects') IS NOT NULL AND to_regclass('public.client_profiles') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.projects p
        LEFT JOIN public.client_profiles cp ON cp.id = p.client_id
        WHERE p.client_id IS NOT NULL AND cp.id IS NULL;
        RAISE NOTICE 'projects.client_id orphan rows: %', v;
        IF v > 0 THEN RAISE WARNING 'projects contains orphan client references'; END IF;
    END IF;

    IF to_regclass('public.proposals') IS NOT NULL AND to_regclass('public.projects') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.proposals pr
        LEFT JOIN public.projects p ON p.id = pr.project_id
        WHERE pr.project_id IS NOT NULL AND p.id IS NULL;
        RAISE NOTICE 'proposals.project_id orphan rows: %', v;
        IF v > 0 THEN RAISE WARNING 'proposals contains orphan project references'; END IF;
    END IF;

    IF to_regclass('public.contracts') IS NOT NULL AND to_regclass('public.projects') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.contracts c
        LEFT JOIN public.projects p ON p.id = c.project_id
        WHERE c.project_id IS NOT NULL AND p.id IS NULL;
        RAISE NOTICE 'contracts.project_id orphan rows: %', v;
        IF v > 0 THEN RAISE WARNING 'contracts contains orphan project references'; END IF;
    END IF;

    IF to_regclass('public.payments') IS NOT NULL AND to_regclass('public.contracts') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.payments pm
        LEFT JOIN public.contracts c ON c.id = pm.contract_id
        WHERE pm.contract_id IS NOT NULL AND c.id IS NULL;
        RAISE NOTICE 'payments.contract_id orphan rows: %', v;
        IF v > 0 THEN RAISE WARNING 'payments contains orphan contract references'; END IF;
    END IF;

    IF to_regclass('public.messages') IS NOT NULL AND to_regclass('public.chat_rooms') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.messages m
        LEFT JOIN public.chat_rooms cr ON cr.id = m.room_id
        WHERE m.room_id IS NOT NULL AND cr.id IS NULL;
        RAISE NOTICE 'messages.room_id orphan rows: %', v;
        IF v > 0 THEN RAISE WARNING 'messages contains orphan chat room references'; END IF;
    END IF;
END $$;

\echo 'Foreign-key validation complete.'
