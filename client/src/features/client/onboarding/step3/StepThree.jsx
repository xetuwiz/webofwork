import { useMemo, useState } from "react";
import { Plus, Trash2 } from "lucide-react";

const emptyPackage = {
  packageType: "Starter",
  price: 149,
  deliveryDays: 3,
  description: "",
};

export default function StepThree({ values, onNext, onBack }) {
  const [packages, setPackages] = useState(
    Array.isArray(values.packages) && values.packages.length > 0
      ? values.packages
      : [
          { ...emptyPackage },
          { ...emptyPackage, packageType: "Pro", price: 349, deliveryDays: 7 },
        ],
  );

  const packageLabels = useMemo(() => ["Starter", "Pro"], []);

  const updatePackage = (index, field, value) => {
    setPackages((current) => current.map((item, itemIndex) => (itemIndex === index ? { ...item, [field]: value } : item)));
  };

  const addPackage = () => {
    setPackages((current) => [...current, { ...emptyPackage, packageType: `Custom ${current.length + 1}` }]);
  };

  const removePackage = (index) => {
    setPackages((current) => (current.length > 1 ? current.filter((_, itemIndex) => itemIndex !== index) : current));
  };

  const onSubmit = () => {
    if (packages.length === 0) return;
    onNext({ packages });
  };

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div className="space-y-2">
        <p className="text-xs font-semibold uppercase tracking-[0.2em] text-blue-600">Step 3</p>
        <h2 className="text-3xl font-bold text-slate-900">Set your service packages</h2>
        <p className="text-sm text-slate-600">Create clear deliverables and transparent pricing for clients.</p>
      </div>

      <div className="space-y-4 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        {packages.map((pkg, index) => (
          <div key={`${pkg.packageType}-${index}`} className="rounded-2xl border border-slate-200 bg-slate-50 p-4">
            <div className="mb-4 flex items-center justify-between gap-3">
              <select
                value={pkg.packageType}
                onChange={(event) => updatePackage(index, "packageType", event.target.value)}
                className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
              >
                {packageLabels.map((label) => (
                  <option key={label} value={label}>{label}</option>
                ))}
              </select>

              {packages.length > 1 && (
                <button type="button" onClick={() => removePackage(index)} className="inline-flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm font-medium text-red-600 hover:bg-red-100">
                  <Trash2 className="h-4 w-4" /> Remove
                </button>
              )}
            </div>

            <div className="grid gap-4 md:grid-cols-3">
              <div>
                <label className="mb-1 block text-sm font-medium text-slate-700">Price</label>
                <input
                  type="number"
                  min="0"
                  value={pkg.price}
                  onChange={(event) => updatePackage(index, "price", Number(event.target.value))}
                  className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-slate-700">Delivery days</label>
                <input
                  type="number"
                  min="1"
                  value={pkg.deliveryDays}
                  onChange={(event) => updatePackage(index, "deliveryDays", Number(event.target.value))}
                  className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                />
              </div>

              <div>
                <label className="mb-1 block text-sm font-medium text-slate-700">Title</label>
                <input
                  value={pkg.packageType}
                  onChange={(event) => updatePackage(index, "packageType", event.target.value)}
                  className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                />
              </div>
            </div>

            <div className="mt-4">
              <label className="mb-1 block text-sm font-medium text-slate-700">Package description</label>
              <textarea
                rows={3}
                value={pkg.description}
                onChange={(event) => updatePackage(index, "description", event.target.value)}
                className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
                placeholder="What is included in this package?"
              />
            </div>
          </div>
        ))}

        <button type="button" onClick={addPackage} className="inline-flex items-center gap-2 rounded-xl border border-dashed border-slate-300 bg-white px-4 py-2 text-sm font-medium text-slate-700 hover:bg-slate-50">
          <Plus className="h-4 w-4" /> Add package
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
