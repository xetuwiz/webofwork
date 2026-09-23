import { useState } from "react";
import { ImagePlus, X } from "lucide-react";

const emptyPortfolioItem = {
  title: "",
  description: "",
  imageUrl: "",
};

export default function StepFour({ values, onNext, onBack }) {
  const [portfolio, setPortfolio] = useState(Array.isArray(values.portfolio) && values.portfolio.length > 0 ? values.portfolio : [emptyPortfolioItem]);

  const updateItem = (index, field, value) => {
    setPortfolio((current) => current.map((item, itemIndex) => (itemIndex === index ? { ...item, [field]: value } : item)));
  };

  const addItem = () => setPortfolio((current) => [...current, { ...emptyPortfolioItem }]);

  const removeItem = (index) => setPortfolio((current) => current.filter((_, itemIndex) => itemIndex !== index));

  const onSubmit = () => {
    const sanitized = portfolio.filter((item) => item.title || item.description || item.imageUrl);
    if (sanitized.length === 0) {
      onNext({ portfolio: [] });
      return;
    }
    onNext({ portfolio: sanitized });
  };

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="space-y-2">
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-blue-600">Step 4</p>
        <h2 className="text-3xl font-bold text-slate-900">Show your best work</h2>
        <p className="text-sm text-slate-600">Add portfolio case studies or product samples that demonstrate your quality.</p>
      </div>

      <div className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        {portfolio.map((item, index) => (
          <div key={`${item.title || "portfolio"}-${index}`} className="rounded-2xl border border-slate-200 bg-slate-50 p-4">
            <div className="mb-4 flex items-center justify-between">
              <span className="text-sm font-semibold text-slate-700">Project #{index + 1}</span>
              {portfolio.length > 1 && (
                <button type="button" onClick={() => removeItem(index)} className="inline-flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-2.5 py-1.5 text-xs font-medium text-red-600 hover:bg-red-100">
                  <X className="h-3.5 w-3.5" /> Remove
                </button>
              )}
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <div>
                <label className="mb-1 block text-sm font-medium text-slate-700">Project title</label>
                <input
                  value={item.title}
                  onChange={(event) => updateItem(index, "title", event.target.value)}
                  placeholder="Brand identity refresh"
                  className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-slate-700">Image URL</label>
                <input
                  value={item.imageUrl}
                  onChange={(event) => updateItem(index, "imageUrl", event.target.value)}
                  placeholder="https://example.com/project.png"
                  className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                />
              </div>
            </div>

            <div className="mt-4">
              <label className="mb-1 block text-sm font-medium text-slate-700">Short description</label>
              <textarea
                rows={3}
                value={item.description}
                onChange={(event) => updateItem(index, "description", event.target.value)}
                placeholder="What did you deliver and what was the outcome?"
                className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
              />
            </div>
          </div>
        ))}

        <button type="button" onClick={addItem} className="inline-flex items-center gap-2 rounded-xl border border-dashed border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
          <ImagePlus className="h-4 w-4" /> Add portfolio item
        </button>
      </div>

      <div className="flex items-center justify-between pt-2">
        <button type="button" onClick={onBack} className="rounded-xl border border-slate-200 bg-white px-4 py-2.5 text-sm font-medium text-slate-600 hover:bg-slate-100">
          Back
        </button>
        <button type="button" onClick={onSubmit} className="rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700">
          Continue
        </button>
      </div>
    </div>
  );
}
