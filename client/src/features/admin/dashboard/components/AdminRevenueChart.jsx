import { useState } from "react";
import { LineChart, Line, XAxis, YAxis, CartesianGrid, ResponsiveContainer, Tooltip, Area, AreaChart } from "recharts";
import { useRevenueChart } from "../dashboardQueries";

export default function AdminRevenueChart() {
  const [range, setRange] = useState("this_year");
  const { data: chartData, isLoading } = useRevenueChart(range);

  return (
    <div className="bg-surface rounded-xl border border-line-soft p-6 shadow-sm transition-all">
      <div className="flex items-center justify-between mb-6">
        <div>
          <h3 className="text-[15px] font-semibold text-fg">Revenue Overview</h3>
          <p className="text-[12px] text-fg-subtle">Platform earnings via commissions</p>
        </div>
        <select 
          value={range}
          onChange={(e) => setRange(e.target.value)}
          className="text-sm font-medium text-fg-muted bg-surface-2 border border-line rounded-lg px-3 py-1.5 outline-none focus-visible:ring-2 focus-visible:ring-brand cursor-pointer"
        >
          <option value="this_year">This Year</option>
          <option value="last_year">Last Year</option>
          <option value="all_time">All Time</option>
        </select>
      </div>
      
      <div className="h-[300px]">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={chartData} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="var(--surface)" vertical={false} />
            <XAxis 
              dataKey="month" 
              stroke="var(--fg-subtle)" 
              tick={{ fill: "#9CA3AF", fontSize: 12 }}
              axisLine={false}
              tickLine={false}
              dy={10}
            />
            <YAxis 
              stroke="var(--fg-subtle)" 
              tick={{ fill: "#9CA3AF", fontSize: 12 }}
              axisLine={false}
              tickLine={false}
            />
            <Tooltip 
              contentStyle={{ backgroundColor: '#111827', borderRadius: '8px', border: 'none', color: '#fff' }}
              itemStyle={{ color: '#fff', fontSize: '12px' }}
            />
            <Line 
              type="monotone" 
              dataKey="revenue" 
              stroke="var(--brand)" 
              strokeWidth={3}
              dot={{ r: 4, fill: "#3B82F6", strokeWidth: 2, stroke: "#fff" }}
              activeDot={{ r: 6, strokeWidth: 0 }}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}