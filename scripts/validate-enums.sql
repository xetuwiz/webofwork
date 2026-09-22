-- Phase 18: validate-enums.sql
-- Validate the imported data matches the expected domain status enums and USD-only settings.
-- Run from the repository root: psql -d webofwork -f scripts/validate-enums.sql

\set ON_ERROR_STOP on
\echo 'Validating enum/domain values...'

DO $$
DECLARE
    v bigint;
BEGIN
    IF to_regclass('public.users') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.users
        WHERE role NOT IN (1, 2, 3);
        RAISE NOTICE 'invalid users.role values: %', v;
        IF v > 0 THEN RAISE WARNING 'users.role contains unexpected enum values'; END IF;

        SELECT count(*) INTO v
        FROM public.users
        WHERE status NOT IN (1, 2, 3, 4);
        RAISE NOTICE 'invalid users.status values: %', v;
        IF v > 0 THEN RAISE WARNING 'users.status contains unexpected enum values'; END IF;
    END IF;

    IF to_regclass('public.projects') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.projects
        WHERE status NOT IN (0, 1, 2, 3, 4);
        RAISE NOTICE 'invalid projects.status values: %', v;
        IF v > 0 THEN RAISE WARNING 'projects.status contains unexpected enum values'; END IF;

        SELECT count(*) INTO v
        FROM public.projects
        WHERE pricing_type NOT IN (1, 2);
        RAISE NOTICE 'invalid projects.pricing_type values: %', v;
        IF v > 0 THEN RAISE WARNING 'projects.pricing_type contains unexpected enum values'; END IF;
    END IF;

    IF to_regclass('public.proposals') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.proposals
        WHERE status NOT IN (0, 1, 2, 3);
        RAISE NOTICE 'invalid proposals.status values: %', v;
        IF v > 0 THEN RAISE WARNING 'proposals.status contains unexpected enum values'; END IF;
    END IF;

    IF to_regclass('public.contracts') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.contracts
        WHERE status NOT IN (0, 1, 2, 3, 4, 5, 6);
        RAISE NOTICE 'invalid contracts.status values: %', v;
        IF v > 0 THEN RAISE WARNING 'contracts.status contains unexpected enum values'; END IF;
    END IF;

    IF to_regclass('public.payments') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.payments
        WHERE status NOT IN (1, 2, 3, 4);
        RAISE NOTICE 'invalid payments.status values: %', v;
        IF v > 0 THEN RAISE WARNING 'payments.status contains unexpected enum values'; END IF;
    END IF;

    IF to_regclass('public.global_settings') IS NOT NULL THEN
        SELECT count(*) INTO v
        FROM public.global_settings
        WHERE default_currency IS DISTINCT FROM 'USD';
        RAISE NOTICE 'non-USD default currency rows: %', v;
        IF v > 0 THEN RAISE WARNING 'global_settings.default_currency is not USD'; END IF;
    END IF;
END $$;

\echo 'Enum validation complete.'
