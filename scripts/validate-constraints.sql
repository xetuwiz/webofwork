-- Phase 18: validate-constraints.sql
-- Validate the imported data against common uniqueness and numeric invariants.
-- Run from the repository root: psql -d webofwork -f scripts/validate-constraints.sql

\set ON_ERROR_STOP on
\echo 'Validating constraints and invariants...'

DO $$
DECLARE
    v bigint;
BEGIN
    IF to_regclass('public.users') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM (
            SELECT email
            FROM public.users
            WHERE email IS NOT NULL
            GROUP BY email
            HAVING count(*) > 1
        ) dup;
        RAISE NOTICE 'duplicate user emails: %', v;
        IF v > 0 THEN RAISE WARNING 'users contains duplicate email values'; END IF;
    END IF;

    IF to_regclass('public.categories') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM (
            SELECT name
            FROM public.categories
            WHERE name IS NOT NULL
            GROUP BY name
            HAVING count(*) > 1
        ) dup;
        RAISE NOTICE 'duplicate category names: %', v;
        IF v > 0 THEN RAISE WARNING 'categories contains duplicate names'; END IF;
    END IF;

    IF to_regclass('public.reviews') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.reviews
        WHERE rating < 1 OR rating > 5;
        RAISE NOTICE 'invalid review ratings: %', v;
        IF v > 0 THEN RAISE WARNING 'reviews contains rating values outside 1..5'; END IF;
    END IF;

    IF to_regclass('public.projects') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.projects
        WHERE fixed_price < 0 OR min_price < 0 OR max_price < 0;
        RAISE NOTICE 'negative project prices: %', v;
        IF v > 0 THEN RAISE WARNING 'projects contains negative pricing values'; END IF;
    END IF;

    IF to_regclass('public.payments') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.payments
        WHERE amount < 0 OR platform_fee < 0;
        RAISE NOTICE 'negative payment amounts: %', v;
        IF v > 0 THEN RAISE WARNING 'payments contains negative amount values'; END IF;
    END IF;

    IF to_regclass('public.global_settings') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.global_settings
        WHERE commission_percentage < 0 OR min_brief_confidence < 0 OR proposal_screening_threshold < 0 OR match_score_threshold < 0;
        RAISE NOTICE 'negative settings values: %', v;
        IF v > 0 THEN RAISE WARNING 'global_settings contains invalid negative amounts'; END IF;
    END IF;
END $$;

\echo 'Constraint validation complete.'
