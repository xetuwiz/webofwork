export default function AdminStatsCard({ label, value, detail }) {
  return <article className="admin-stat"><span>{label}</span><strong>{value}</strong>{detail && <small>{detail}</small>}</article>;
}