import React from 'react';
import { Link, useParams } from 'react-router-dom';

export default function ProjectHeader({ title, budget, proposalCount, postedTime }) {
  const {id} = useParams()
  return (
    <div className="rounded-lg border border-line bg-surface p-6 shadow-sm">
      <div className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex-1">
          <h2 className="text-lg font-semibold text-fg lg:text-xl capitalize">{title}</h2>
          <div className="mt-4 flex flex-wrap gap-x-6 gap-y-2 text-sm">
            <div>
              <span className="text-fg-muted">Budget: </span>
              <span className="font-medium text-fg">{budget}</span>
            </div>
            <div>
              <span className="text-fg-muted">Proposals: </span>
              <span className="font-medium text-fg">{proposalCount}</span>
            </div>
            <div className="text-fg-muted">{postedTime}</div>
          </div>
        </div>
        <Link to={`/my-projects/${id}`} className="rounded-lg border border-blue-500 text-center bg-transparent px-6 py-2.5 text-sm font-medium text-brand transition hover:bg-brand-hover-soft">
          View Post
        </Link>
      </div>
    </div>
  );
}