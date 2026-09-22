-- Phase 18: seed-data.sql
-- Seed core platform content and ensure default settings are USD-based.
-- Run from the repository root: psql -d webofwork -f scripts/seed-data.sql

\set ON_ERROR_STOP on
\echo 'Seeding base platform data...'

INSERT INTO categories (id, name)
VALUES
    (1, 'Design'),
    (2, 'Development'),
    (3, 'Marketing'),
    (4, 'Writing'),
    (5, 'Business')
ON CONFLICT (id) DO UPDATE
SET name = EXCLUDED.name;

INSERT INTO faqs (id, question, answer, category, sort_order, is_active, created_at, updated_at)
VALUES
    (1,
     'How do I get started on WebOfWork?',
     'Create your account, complete your profile, and then either post a brief or browse active projects.',
     1,
     1,
     true,
     now(),
     now()),
    (2,
     'How do payments work?',
     'All platform transactions use USD and are escrowed until the contract is completed or released.',
     4,
     2,
     true,
     now(),
     now()),
    (3,
     'How are matching and screening handled?',
     'WebOfWork compares skill fit, project alignment, and platform thresholds before the proposal is advanced.',
     2,
     3,
     true,
     now(),
     now())
ON CONFLICT (id) DO UPDATE
SET question = EXCLUDED.question,
    answer = EXCLUDED.answer,
    category = EXCLUDED.category,
    sort_order = EXCLUDED.sort_order,
    is_active = EXCLUDED.is_active,
    updated_at = now();

INSERT INTO global_settings (
    id,
    support_email,
    commission_percentage,
    pay_pal_email,
    min_brief_confidence,
    proposal_screening_threshold,
    match_score_threshold,
    default_currency,
    created_at,
    updated_at
)
VALUES (
    1,
    'support@webofwork.lk',
    10.00,
    'payments@webofwork.lk',
    0.60,
    0.50,
    0.60,
    'USD',
    now(),
    now()
)
ON CONFLICT (id) DO UPDATE
SET support_email = EXCLUDED.support_email,
    commission_percentage = EXCLUDED.commission_percentage,
    pay_pal_email = EXCLUDED.pay_pal_email,
    min_brief_confidence = EXCLUDED.min_brief_confidence,
    proposal_screening_threshold = EXCLUDED.proposal_screening_threshold,
    match_score_threshold = EXCLUDED.match_score_threshold,
    default_currency = EXCLUDED.default_currency,
    updated_at = now();

\echo 'Seed data complete. Default currency is USD.'
