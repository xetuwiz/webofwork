import React from 'react'
import { Link } from 'react-router-dom';

function AdminFooter() {
  const d = new Date();
let year = d.getFullYear();
  return (
    <div className="text-center py-6">
        <p className="text-xs text-fg-subtle">© {year} <Link to={'/'}><span className="text-fg">Web</span><span className="text-brand">OfWork.</span></Link>  Admin Dashboard</p>
      </div>
  )
}

export default AdminFooter