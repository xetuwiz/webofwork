import { BarChart, Bar, XAxis, YAxis, Tooltip, CartesianGrid, ResponsiveContainer } from "recharts";
import { useProposalsChart } from "../dashboardQueries";

const CustomTooltip = ({ active, payload, label }) => {
  if (active && payload && payload.length) {
    return (
      <div className="bg-inverse/95 backdrop-blur-sm text-white p-3 rounded-xl shadow-2xl border border-inverse-fg/20 min-w-[120px]">
        <p className="text-[10px] uppercase tracking-wider text-fg-subtle mb-2 font-bold">{label}</p>
        <div className="space-y-1.5">
          {payload.map((item, index) => (
            <div key={index} className="flex items-center justify-between gap-4">
              <div className="flex items-center gap-2">
                <div 
                  className="w-1.5 h-1.5 rounded-full" 
                  style={{ backgroundColor: item.fill }}
                />
                <span className="text-[11px] text-fg-subtle capitalize">{item.name}</span>
              </div>
              <span className="text-[12px] font-bold font-mono">{item.value}</span>
            </div>
          ))}
        </div>
      </div>
    );
  }
  return null;
};

export default function AdminProposalsChart() {
  const { data } = useProposalsChart();

  return (
    <div className="bg-surface rounded-2xl border border-line-soft p-6 shadow-sm hover:shadow-md transition-shadow duration-slow">
      <div className="flex flex-row items-center justify-between mb-8">
        <div>
          <h3 className="text-[16px] font-bold text-fg tracking-tight">Activity Overview</h3>
          <p className="text-[12px] text-fg-subtle">Projects vs Proposals</p>
        </div>
        <div className="flex items-center gap-4 bg-surface-2/80 p-1.5 rounded-lg border border-line-soft">
          <div className="flex items-center gap-1.5 px-2">
            <div className="w-2 h-2 rounded-full bg-brand"></div>
            <span className="text-[11px] font-medium text-fg-muted">Projects</span>
          </div>
          <div className="flex items-center gap-1.5 px-2">
            <div className="w-2 h-2 rounded-full bg-brand-soft"></div>
            <span className="text-[11px] font-medium text-fg-muted">Proposals</span>
          </div>
        </div>
      </div>
      
      <div className="h-[280px] w-full">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart 
            data={data} 
            margin={{ top: 0, right: 0, left: -25, bottom: 0 }}
            barGap={8}
          >
            <CartesianGrid 
              strokeDasharray="4 4" 
              stroke="var(--surface)" 
              vertical={false} 
            />
            <XAxis 
              dataKey="date" 
              stroke="var(--fg-subtle)" 
              tick={{ fill: "#94A3B8", fontSize: 11, fontWeight: 500 }}
              axisLine={false}
              tickLine={false}
              dy={10}
            />
            <YAxis 
              stroke="var(--fg-subtle)" 
              tick={{ fill: "#94A3B8", fontSize: 11, fontWeight: 500 }}
              axisLine={false}
              tickLine={false}
              tickCount={6}
            />
            <Tooltip 
              content={<CustomTooltip />}
              cursor={{ fill: '#F8FAFC', radius: 8 }}
              shared={true}
            />
            <Bar 
              dataKey="projects" 
              fill="var(--brand)" 
              radius={[6, 6, 0, 0]} 
              barSize={14}
              animationDuration={1500}
              animationBegin={200}
            />
            <Bar 
              dataKey="proposals" 
              fill="var(--brand-line)" 
              radius={[6, 6, 0, 0]} 
              barSize={14}
              animationDuration={1500}
              animationBegin={400}
            />
          </BarChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}