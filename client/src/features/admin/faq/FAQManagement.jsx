import React, { useState } from 'react';
import { useAdminFAQs } from './faqQueries';
import { useFAQMutations } from './faqMutations';
import { Plus, Edit2, Trash2, X, AlertTriangle, Loader2 } from 'lucide-react';
import Pagination from '../../../components/common/Pagination';
import EmptyState from '../../../components/common/EmptyState';
import { useListState, toQueryParams } from '../../../hooks/useListState';
import { Field } from "../../../components/common/Field";
import Dialog, { DialogClose } from "../../../components/common/Dialog";

const CATEGORIES = [
  { id: 'general', label: 'General' },
  { id: 'freelancer', label: 'Freelancer' },
  { id: 'client', label: 'Client' },
  { id: 'payment', label: 'Payment & Security' }
];

export default function FAQManagement() {
  // Page and page size live in the URL, so the view is shareable and a refresh
  // keeps the admin where they were.
  const { page, pageSize, setPage, setPageSize } = useListState();

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [currentFaq, setCurrentFaq] = useState(null);
  const [formData, setFormData] = useState({ question: '', answer: '', category: 'general' });

  const { data, isLoading } = useAdminFAQs(toQueryParams({}, page, pageSize));
  const { createMutation, updateMutation, deleteMutation } = useFAQMutations();

  const handleOpenModal = (faq = null) => {
    if (faq) {
      setFormData({ question: faq.question, answer: faq.answer, category: faq.category });
      setCurrentFaq(faq);
    } else {
      setFormData({ question: '', answer: '', category: 'general' });
      setCurrentFaq(null);
    }
    setIsModalOpen(true);
  };

  const handleSubmit = (e) => {
    e.preventDefault();
    if (currentFaq) {
      updateMutation.mutate({ id: currentFaq.id, ...formData }, { onSuccess: () => setIsModalOpen(false) });
    } else {
      createMutation.mutate(formData, { onSuccess: () => setIsModalOpen(false) });
    }
  };

  const handleDelete = () => {
    deleteMutation.mutate(currentFaq.id, { onSuccess: () => setIsDeleteModalOpen(false) });
  };

  if (isLoading) return <div className="flex justify-center p-20"><Loader2 className="animate-spin text-brand" /></div>;

  return (
    <div className="p-4 md:p-8 bg-surface-2 min-h-screen">
      {/* Heading scale now matches the sibling content pages
          (PrivacyPolicy, TermAndConditions), which use `text-xl sm:text-[26px]`
          with `text-fg-muted` body. This was `text-2xl` + `text-fg-subtle`, so
          three adjacent admin pages disagreed on both the type size and how
          faint the subtitle was. */}
      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8">
        <div>
          <h1 className="text-xl sm:text-[26px] font-bold text-fg leading-tight">FAQ Management</h1>
          <p className="text-sm text-fg-muted">Manage help center questions and categories</p>
        </div>
        <button 
          onClick={() => handleOpenModal()}
          className="flex items-center gap-2 bg-brand text-brand-fg px-5 py-2.5 rounded-lg hover:bg-brand-hover transition-colors shadow-sm"
        >
          <Plus size={18} /> Add New FAQ
        </button>
      </div>

      <div className="bg-surface rounded-xl border border-line-soft shadow-sm overflow-hidden">
        <table className="w-full table-fixed border-collapse">
          <thead>
            <tr className="border-b border-line-soft text-fg-subtle text-[11px] uppercase tracking-wider bg-surface-2/50">
              <th className="py-4 px-6 font-semibold text-left w-[25%]">Question</th>
              <th className="py-4 px-6 font-semibold text-left w-[45%]">Answer</th>
              <th className="py-4 px-6 font-semibold text-left w-[15%]">Category</th>
              <th className="py-4 px-6 font-semibold text-right w-[15%]">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-50">
            {data?.results?.map((faq) => (
              <tr key={faq.id} className="hover:bg-surface-2/80 transition-colors">
                <td className="py-5 px-6 align-top">
                  <p className="text-sm font-semibold text-fg break-words leading-relaxed">
                    {faq.question}
                  </p>
                </td>
                <td className="py-5 px-6 align-top">
                  <p className="text-sm text-fg-muted break-words leading-relaxed whitespace-pre-wrap">
                    {faq.answer}
                  </p>
                </td>
                <td className="py-5 px-6 align-top">
                  <span className="inline-flex px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-brand-soft text-brand uppercase tracking-tight">
                    {faq.category}
                  </span>
                </td>
                <td className="py-5 px-6 align-top text-right">
                  <div className="flex justify-end gap-1">
                    <button 
                      onClick={() => handleOpenModal(faq)} 
                      className="p-2 text-fg-subtle hover:text-brand hover:bg-brand-hover-soft rounded-lg transition-all"
                    >
                      <Edit2 size={16} />
                    </button>
                    <button 
                      onClick={() => { setCurrentFaq(faq); setIsDeleteModalOpen(true); }} 
                      className="p-2 text-fg-subtle hover:text-danger hover:bg-danger-soft rounded-lg transition-all"
                    >
                      <Trash2 size={16} />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        
        {(!data?.results || data.results.length === 0) && (
          <EmptyState
            variant="first-use"
            title="No FAQs yet"
            description="Add the first question and answer to get started."
          />
        )}

        {/* Replaces a hand-rolled prev/next bar whose counter read
            `results.length` -- this page's row count, not the range start. */}
        <div className="px-6 py-4 border-t border-line-soft bg-surface">
          <Pagination
            page={page}
            pageSize={pageSize}
            count={data?.count ?? 0}
            onPageChange={setPage}
            onPageSizeChange={setPageSize}
            itemNoun="FAQs"
            className="pt-0"
          />
        </div>
      </div>

      {/* Was a hand-rolled scrim: an absolutely-positioned div with onClick and
          a sibling panel. That gave no Escape handling (WCAG 2.1.2), no focus
          trap, no focus restoration, and no role="dialog" -- so the heading was
          never announced as the dialog's name. <Dialog> supplies all of it. */}
      <Dialog
        open={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        title={currentFaq ? 'Edit FAQ' : 'Create New FAQ'}
        size="lg"
        className="p-0 overflow-hidden"
      >
        <div className="flex justify-between items-center p-6 border-b border-line-soft">
          <h2 className="text-xl font-bold text-fg">{currentFaq ? 'Edit FAQ' : 'Create New FAQ'}</h2>
          <DialogClose className="text-fg-subtle hover:text-fg-muted hover:bg-surface-3 rounded-full">
            <X size={20} aria-hidden="true" />
          </DialogClose>
        </div>
        <form onSubmit={handleSubmit} className="p-6 space-y-5">
              <div className="space-y-1.5">
                <Field label="Category">
                  {(field) => (
                    <select
                      {...field}
                      className="w-full border border-line rounded-xl px-4 py-2.5 focus-visible:ring-2 focus-visible:ring-brand/20 focus-visible:border-brand outline-none transition-all appearance-none bg-surface-2/50"
                      value={formData.category}
                      onChange={(e) => setFormData({...formData, category: e.target.value})}
                    >
                      {CATEGORIES.map(cat => <option key={cat.id} value={cat.id}>{cat.label}</option>)}
                    </select>
                  )}
                </Field>
              </div>
              <div className="space-y-1.5">
                <Field label="Question">
                  {(field) => (
                    <input
                      {...field}
                      required
                      className="w-full border border-line rounded-xl px-4 py-2.5 focus-visible:ring-2 focus-visible:ring-brand/20 focus-visible:border-brand outline-none transition-all bg-surface-2/50"
                      value={formData.question}
                      onChange={(e) => setFormData({...formData, question: e.target.value})}
                      placeholder="Enter the question..."
                    />
                  )}
                </Field>
              </div>
              <div className="space-y-1.5">
                <Field label="Answer">
                  {(field) => (
                    <textarea
                      {...field}
                      required
                      rows={5}
                      className="w-full border border-line rounded-xl px-4 py-2.5 focus-visible:ring-2 focus-visible:ring-brand/20 focus-visible:border-brand outline-none transition-all bg-surface-2/50 resize-none"
                      value={formData.answer}
                      onChange={(e) => setFormData({...formData, answer: e.target.value})}
                      placeholder="Enter the detailed answer..."
                    />
                  )}
                </Field>
              </div>
              <button
                type="submit"
                disabled={createMutation.isLoading || updateMutation.isLoading}
                className="w-full bg-brand text-brand-fg py-3 rounded-xl font-bold hover:bg-brand-hover disabled:opacity-50 transition-all shadow-md shadow-blue-100 active:scale-[0.98]"
              >
                {currentFaq ? 'Save Changes' : 'Create FAQ'}
              </button>
        </form>
      </Dialog>

      {/* Second hand-rolled modal in this file. Same defects. */}
      <Dialog
        open={isDeleteModalOpen}
        onClose={() => setIsDeleteModalOpen(false)}
        title="Delete FAQ?"
        description="This action cannot be undone. This question will be permanently removed from the platform."
        size="sm"
        className="p-8 text-center"
      >
            <div className="w-16 h-16 bg-danger-soft text-danger rounded-full flex items-center justify-center mx-auto mb-4 border-4 border-red-50/50">
              <AlertTriangle size={32} aria-hidden="true" />
            </div>
            <h2 className="text-xl font-bold text-fg mb-2">Delete FAQ?</h2>
            <p className="text-fg-subtle mb-8 text-sm leading-relaxed">This action cannot be undone. This question will be permanently removed from the platform.</p>
            <div className="flex gap-3">
              <button onClick={() => setIsDeleteModalOpen(false)} className="flex-1 px-4 py-2.5 border border-line rounded-xl font-bold text-fg-muted hover:bg-surface-2 transition-all">Cancel</button>
              <button
                onClick={handleDelete}
                className="flex-1 px-4 py-2.5 bg-danger text-danger-fg rounded-xl font-bold hover:bg-danger transition-all shadow-md shadow-red-100 active:scale-[0.98]"
              >
                Delete
              </button>
            </div>
      </Dialog>
    </div>
  );
}
