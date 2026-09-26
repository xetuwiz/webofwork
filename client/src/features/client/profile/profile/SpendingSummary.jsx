import { 
  DollarSign, 
  Briefcase, 
  Clock, 
  TrendingUp 
} from "lucide-react";
import { useClientSpendingSummary } from "./profileQueries";

export default function SpendingSummary() {
  const { data, isLoading, isError } = useClientSpendingSummary();

  if (isLoading) return <div className="p-10 text-center">Loading Summary...</div>;
  if (isError) return <div className="p-10 text-center text-danger-soft-fg">Error loading data</div>;

  const stats = [
    {
      label: "Total Spent",
      value: `$${Number(data.total_spent).toLocaleString()}`,
      icon: DollarSign,
      bgColor: "bg-brand-soft",
      iconColor: "text-fg",
    },
    {
      label: "Projects Completed",
      value: data.projects_completed,
      icon: Briefcase,
      bgColor: "bg-success-soft",
      iconColor: "text-success",
    },
    {
      label: "Ongoing Projects",
      value: data.ongoing_projects,
      icon: Clock,
      bgColor: "bg-warning-soft",
      iconColor: "text-warning",
    },
    {
      label: "Avg. per Project",
      value: `$${Number(data.avg_per_project).toLocaleString()}`,
      icon: TrendingUp,
      bgColor: "bg-brand-soft",
      iconColor: "text-[#9333EA]",
    },
  ];

  return (
    <div className="min-h-screen bg-surface px-4 py-8 sm:px-6 lg:px-8">
      <div className="mx-auto max-w-7xl">
        <h1 className="mb-8 text-[26px] font-bold leading-9 text-fg">
          Spending Summary
        </h1>

        <div className="mb-6 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4 lg:gap-[67px]">
          {stats.map((stat, index) => (
            <div key={index} className="rounded-xl border border-line-soft bg-surface p-6 shadow-sm">
              <div className={`mb-4 flex h-12 w-12 items-center justify-center rounded-lg ${stat.bgColor}`}>
                <stat.icon className={`h-6 w-6 ${stat.iconColor}`} strokeWidth={2} />
              </div>
              <div className="mb-1 text-xl font-bold leading-8 text-fg">
                {stat.value}
              </div>
              <div className="text-xs leading-5 text-fg-subtle">
                {stat.label}
              </div>
            </div>
          ))}
        </div>

        <div className="rounded-xl border border-line-soft bg-surface p-6 shadow-sm">
          <h2 className="mb-6 text-[15px] font-semibold leading-7 text-fg">
            Recent Projects
          </h2>

          <div className="space-y-4">
            {data.recent_projects?.map((project) => (
              <div key={project.id} className="flex flex-col gap-3 rounded-lg border border-line-soft p-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex-1">
                  <h3 className="mb-1 text-sm font-medium leading-6 text-fg">
                    {project.project_title}
                  </h3>
                  <p className="text-xs leading-5 text-fg-subtle">
                    with {project.freelancer_name}
                  </p>
                </div>
                <div className="flex items-center gap-4">
                  <span className="text-sm font-semibold leading-6 text-fg">
                    ${Number(project.total_amount).toLocaleString()}
                  </span>
                  <span className={`inline-flex rounded-full px-3 py-1 text-[10px] font-medium leading-4 capitalize ${
                    project.status === 'completed' 
                      ? "bg-success-soft text-success-soft-fg" 
                      : "bg-brand-soft text-brand-soft-fg"
                  }`}>
                    {project.status.replace('_', ' ')}
                  </span>
                </div>
              </div>
            ))}

            {data.recent_projects?.length === 0 && (
              <div className="py-4 text-center text-sm text-fg-subtle">
                No recent projects found.
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}