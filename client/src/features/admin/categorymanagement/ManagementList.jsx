import { GripVertical, Pencil, Plus, Trash2, X } from "lucide-react";
import Pagination from "../../../components/common/Pagination";
import EmptyState from "../../../components/common/EmptyState";

export const ManagementList = ({
  title,
  itemNoun,
  icon: Icon,
  items,
  count,
  iconColor,
  onAdd,
  onEdit,
  onDelete,
  searchValue,
  onSearchChange,
  onSearchSubmit,
  hasActiveSearch,
  onClearSearch,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange,
}) => {
  return (
    <div className="mb-12">
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-4">
          <div className="p-3 rounded-xl bg-surface shadow">
            <Icon className={`w-6 h-6 text-${iconColor}-600`} />
          </div>
          <div>
            <h2 className="text-xl font-bold text-fg">{title}</h2>
            {/* Was `{items.length} {title.toLowerCase()}`, which is this page's
                row count presented as if it were the total. On page 1 of 5 with
                12 per page it claimed there were 12 of 42. */}
            <p className="text-sm text-fg-subtle">
              {count} {itemNoun} in total
            </p>
          </div>
        </div>

        <button
          type="button"
          onClick={onAdd}
          className="flex items-center gap-2 px-4 py-2 bg-brand text-brand-fg rounded-lg hover:bg-brand-hover/30 hover:text-fg transition-colors text-sm font-semibold shadow-sm"
        >
          <Plus size={18} />
          Add {title}
        </button>
      </div>

      <div className="mb-4 relative">
        <input
          value={searchValue}
          onChange={(e) => onSearchChange(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") {
              onSearchSubmit();
            }
          }}
          placeholder={`Search ${itemNoun}...`}
          aria-label={`Search ${itemNoun}`}
          className="w-full h-10 px-4 rounded-lg border border-line text-sm outline-none focus-visible:ring-2 focus-visible:ring-brand"
        />
        {searchValue && (
          <button
            type="button"
            onClick={onClearSearch}
            aria-label={`Clear ${itemNoun} search`}
            className="absolute right-3 top-1/2 -translate-y-1/2 p-1 rounded-full hover:bg-surface-3 transition-colors"
          >
            <X size={14} className="text-fg-subtle" />
          </button>
        )}
      </div>

      <div className="bg-surface rounded-2xl border border-line-soft overflow-hidden shadow-sm">
        <div className="divide-y divide-gray-50">
          {items.map((item) => (
            <div
              key={item.id}
              className="group flex items-center justify-between px-6 py-5 hover:bg-surface-2/50 transition-colors"
            >
              <div className="flex items-center gap-4">
                <GripVertical className="w-5 h-5 text-fg-subtle" />
                <span className="text-[15px] font-medium text-fg-muted">
                  {item.name}
                </span>
              </div>

              <div className="flex items-center gap-3 opacity-0 group-hover:opacity-100 transition-opacity">
                <button
                  type="button"
                  onClick={() => onEdit(item)}
                  aria-label={`Edit ${item.name}`}
                  className="p-2 text-fg-subtle hover:text-brand hover:bg-brand-hover-soft rounded-lg"
                >
                  <Pencil size={18} />
                </button>
                <button
                  type="button"
                  onClick={() => onDelete(item)}
                  aria-label={`Delete ${item.name}`}
                  className="p-2 text-fg-subtle hover:text-danger hover:bg-danger-soft rounded-lg"
                >
                  <Trash2 size={18} />
                </button>
              </div>
            </div>
          ))}

          {/* Contextual. With a search term applied, an empty list means the term
              matched nothing -- not that there are no categories at all. */}
          {items.length === 0 && (
            <EmptyState
              variant={hasActiveSearch ? "no-match" : "first-use"}
              title={
                hasActiveSearch ? `No ${itemNoun} match your search` : undefined
              }
              className="py-12"
              onClearFilters={onClearSearch}
            />
          )}
        </div>
      </div>

      <Pagination
        page={page}
        pageSize={pageSize}
        count={count}
        onPageChange={onPageChange}
        onPageSizeChange={onPageSizeChange}
        itemNoun={itemNoun}
        className="mt-8 pt-0"
      />
    </div>
  );
};
