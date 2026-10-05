import React, { useState, useEffect } from 'react';
import { useFAQs } from './faqQueries';
import { ChevronDown, Mail, Loader2 } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useSelector } from 'react-redux';
import Pagination from '../Pagination';
import EmptyState from '../EmptyState';
import { useListState } from '../../../hooks/useListState';

const CATEGORIES = [
  { id: '', label: 'All' },
  { id: 'general', label: 'General' },
  { id: 'freelancer', label: 'Freelancers' },
  { id: 'client', label: 'Clients' },
  { id: 'payment', label: 'Payments' },
];

export default function FAQPage() {
  // Category and page live in the URL, so a reader can share "the Billing
  // questions, page 2" and the back button steps back through category changes
  // rather than pages.
  const { page, pageSize, filters, set: setFilter, setPage, setPageSize, resetFilters } =
    useListState({ defaults: { category: '' } });
  const category = filters.category;

  // Which accordion is open is deliberately NOT in the URL: it is ephemeral UI
  // state that should not clutter a shared link or survive a back navigation.
  const [openId, setOpenId] = useState(null);

  const { data, isLoading, isFetching } = useFAQs({ category, page, page_size: pageSize });
  const user = useSelector(state => state.auth.user)
  useEffect(() => {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }, [page]);

  if (isLoading) return (
    <div className="min-h-screen flex items-center justify-center bg-surface-2">
      <Loader2 className="w-10 h-10 animate-spin text-brand" />
    </div>
  );

  return (
    <div className="min-h-screen bg-surface-2 py-16 px-4">
      <div className="max-w-4xl mx-auto">
        <div className="mb-12">
          <h1 className="text-[40px] font-bold text-fg mb-2">Frequently Asked Questions</h1>
          <p className="text-fg-subtle text-lg">Find quick answers to common questions.</p>
        </div>

        <div className="flex flex-wrap gap-2 mb-8">
          {CATEGORIES.map((cat) => (
            <button
              key={cat.id}
              onClick={() => {
                setFilter('category', cat.id);
                setOpenId(null);
              }}
              className={`px-6 py-2 rounded-full text-sm font-medium transition-all ${category === cat.id
                  ? 'bg-brand text-white shadow-md'
                  : 'bg-surface text-fg-muted hover:bg-surface-3 border border-line'
                }`}
            >
              {cat.label}
            </button>
          ))}
        </div>

        <div className="flex items-center justify-between mb-6">
          <h2 className="text-xl font-bold text-fg">Popular Questions</h2>
          {isFetching && <Loader2 className="w-5 h-5 animate-spin text-brand" />}
        </div>

        <div className="space-y-3 mb-10 min-h-[400px]">
          {data?.results?.map((faq) => (
            <div key={faq.id} className="bg-surface rounded-xl border border-line-soft overflow-hidden shadow-sm">
              <button
                onClick={() => setOpenId(openId === faq.id ? null : faq.id)}
                className="w-full flex items-center justify-between p-5 text-left hover:bg-surface-2 transition-colors"
              >
                <span className="font-semibold text-fg text-[15px]">{faq.question}</span>
                <ChevronDown
                  className={`text-fg-subtle transition-transform duration-slow ${openId === faq.id ? 'rotate-180' : ''}`}
                  size={20}
                />
              </button>

              <div
                className={`transition-all duration-slow ease-emphasized ${openId === faq.id ? 'max-h-[1000px] opacity-100' : 'max-h-0 opacity-0'
                  } overflow-hidden`}
              >
                <div className="p-5 pt-0 text-fg-muted text-[14px] leading-relaxed border-t border-line-soft mt-2">
                  {faq.answer}
                </div>
              </div>
            </div>
          ))}

          {data?.results?.length === 0 && (
            <EmptyState
              variant={category ? 'no-match' : 'first-use'}
              title={category ? 'No questions in this category yet' : undefined}
              onClearFilters={resetFilters}
              className="bg-surface rounded-xl border border-dashed border-line"
            />
          )}
        </div>

        {/* Replaces a hand-rolled prev/next bar whose counter read
            `results.length` -- this page's row count, not the range start -- so on
            page 3 it said "Showing 5 of 47". */}
        <Pagination
          page={page}
          pageSize={pageSize}
          count={data?.count ?? 0}
          onPageChange={setPage}
          onPageSizeChange={setPageSize}
          itemNoun="questions"
          className="mb-10 mt-8"
        />

        <div className="bg-surface rounded-2xl p-8 border border-line-soft shadow-sm flex flex-col md:flex-row items-center gap-6">
          <div className="w-16 h-16 bg-brand rounded-xl flex items-center justify-center shadow-lg shadow-blue-200">
            <Mail className="text-white w-8 h-8" />
          </div>
          <div className="flex-1 text-center md:text-left">
            <h3 className="text-xl font-bold text-fg mb-1">Need More Help?</h3>
            <p className="text-fg-subtle text-sm">Can't find what you're looking for? Our support team is here to help you with any questions or issues.</p>
            <div className="mt-4 flex flex-col md:flex-row items-center gap-4">
              <Link to={user?.current_role === 'client' ? '/support' : '/freelancer/support'} className="bg-brand text-brand-fg px-6 py-2 rounded-lg text-sm font-bold hover:bg-brand-hover transition-colors">
                Contact Support
              </Link>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}