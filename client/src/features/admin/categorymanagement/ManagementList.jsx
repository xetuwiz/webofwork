import { ChevronLeft, ChevronRight, GripVertical, Pencil, Plus, Trash2 } from "lucide-react";

const iconTone = {
  blue: "text-blue-600",
  purple: "text-purple-600",
};

export function ManagementList({
  title,
  icon,
  items,
  iconColor,
  onAdd,
  onEdit,
  onDelete,
  searchValue,
  onSearchChange,
  onSearchSubmit,
  onNext,
  onPrev,
  hasNext,
  hasPrev,
  currentPage,
  totalCount,
}) {
  const IconComponent = icon;

  return (
    <div className="mb-12">
      <div className="mb-6 flex items-center justify-between gap-4">
        <div className="flex items-center gap-4">
          <div className="rounded-xl bg-white p-3 shadow-sm">
            <IconComponent className={`h-6 w-6 ${iconTone[iconColor] || "text-slate-600"}`} />
          </div>
          <div>
            <h2 className="text-xl font-bold text-slate-900">{title}</h2>
            <p className="text-sm text-slate-500">{items.length} {title.toLowerCase()}</p>
          </div>
        </div>

        <button
          type="button"
          onClick={onAdd}
          className="flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition-colors hover:bg-blue-700"
        >
          <Plus size={18} />
          Add {title}
        </button>
      </div>

      <div className="mb-4">
        <input
          value={searchValue}
          onChange={(event) => onSearchChange(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              onSearchSubmit();
            }
          }}
          placeholder={`Search ${title.toLowerCase()}...`}
          className="h-10 w-full rounded-lg border border-slate-200 bg-white px-4 text-sm text-slate-700 outline-none transition focus:border-blue-500 focus:ring-2 focus:ring-blue-100"
        />
      </div>

      <div className="overflow-hidden rounded-2xl border border-slate-100 bg-white shadow-sm">
        <div className="divide-y divide-slate-100">
          {items.map((item) => (
            <div
              key={item.id}
              className="group flex items-center justify-between px-6 py-5 transition-colors hover:bg-slate-50/80"
            >
              <div className="flex items-center gap-4">
                <GripVertical className="h-5 w-5 text-slate-300" />
                <span className="text-[15px] font-medium text-slate-700">{item.name}</span>
              </div>

              <div className="flex items-center gap-3 opacity-0 transition-opacity group-hover:opacity-100">
                <button
                  type="button"
                  onClick={() => onEdit(item)}
                  className="rounded-lg p-2 text-slate-400 transition hover:bg-blue-50 hover:text-blue-600"
                  aria-label={`Edit ${item.name}`}
                >
                  <Pencil size={18} />
                </button>
                <button
                  type="button"
                  onClick={() => onDelete(item)}
                  className="rounded-lg p-2 text-slate-400 transition hover:bg-red-50 hover:text-red-600"
                  aria-label={`Delete ${item.name}`}
                >
                  <Trash2 size={18} />
                </button>
              </div>
            </div>
          ))}

          {items.length === 0 && (
            <div className="px-6 py-10 text-center text-sm text-slate-500">No {title.toLowerCase()} found</div>
          )}
        </div>
      </div>

      {totalCount > 0 && (
        <div className="mt-8 flex flex-col items-center justify-between gap-4 pt-2 md:flex-row">
          <p className="text-sm text-slate-600">Showing {items.length} of {totalCount} results</p>
          <nav className="flex items-center rounded-md shadow-sm">
            <button
              type="button"
              disabled={!hasPrev}
              onClick={onPrev}
              className="flex h-9 w-9 items-center justify-center rounded-l-md border border-slate-300 bg-white transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
            >
              <ChevronLeft className="h-5 w-5 text-slate-400" />
            </button>
            <div className="flex h-9 items-center border-y border-blue-600 bg-blue-600 px-4 text-sm font-semibold text-white">
              {currentPage}
            </div>
            <button
              type="button"
              disabled={!hasNext}
              onClick={onNext}
              className="flex h-9 w-9 items-center justify-center rounded-r-md border border-slate-300 bg-white transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-50"
            >
              <ChevronRight className="h-5 w-5 text-slate-400" />
            </button>
          </nav>
        </div>
      )}
    </div>
  );
}
