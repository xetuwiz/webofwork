export default function AdminStatsCard({
  title,
  value,
  change,
  changeType,
  icon: Icon,
  iconBgColor = "bg-brand-soft",
  iconColor = "text-brand",
  changeLabel = "vs last month",
}) {
  return (
    <div className="bg-surface rounded-xl border border-line-soft p-6 shadow-sm">
      <div className="flex items-start justify-between mb-6">
        <div>
          <p className="text-xs font-medium text-fg-subtle mb-1">
            {title}
          </p>
          <h3 className="text-xl font-bold text-fg">
            {value}
          </h3>
        </div>
    
        <div
          className={`w-10 h-10 rounded-lg flex items-center justify-center ${iconBgColor}`}
        >
          <Icon className={`w-5 h-5 ${iconColor}`} strokeWidth={2} />
        </div>
      </div>

      <div className="flex items-center gap-2">
        <span
          className={`text-[10px] font-medium px-2 py-0.5 rounded-full ${
            changeType === "positive"
              ? "bg-success-soft text-success"
              : "bg-danger-soft text-danger"
          }`}
        >
          {changeType === "positive" && "+"}
          {change}
        </span>

        <span className="text-[10px] text-fg-subtle">
          {changeLabel}
        </span>
      </div>
    </div>
  );
}
