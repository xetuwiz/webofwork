import { Calendar, DollarSign, User, FileX } from 'lucide-react'
import React from 'react'
import { useMyContracts } from '../../contracts/contractsQueries'
import { formatDateDMY } from '../../../../utils/date'
import { Link } from 'react-router-dom'

function ActiveContacts() {
  const { data, isLoading } = useMyContracts()
  const contracts = data?.results || []
  const activeContracts = contracts
    .filter(contract => contract.status === 'active' || contract.status === 'disputed')
    .slice(0, 4)
  
  const getDueDate = (signedAt, days) => {
    if (!signedAt || !days) return 'N/A';
    
    const signedDate = new Date(signedAt);
    const dueDate = new Date(signedDate);
    dueDate.setDate(signedDate.getDate() + parseInt(days));

    return formatDateDMY(dueDate.toISOString());
  };

  if (isLoading) {
    return (
      <div className="mb-8 w-full max-w-6xl">
        <h2 className="text-xl md:text-[25px] font-semibold text-fg leading-7 mb-4 px-1">
          Active Contracts
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 md:gap-6">
          {[...Array(2)].map((_, i) => (
            <div key={i} className="h-40 bg-surface-3 shimmer rounded-xl border border-line-soft"></div>
          ))}
        </div>
      </div>
    )
  }

  return (
    <div className="mb-8 w-full max-w-6xl">
      <h2 className="text-xl md:text-[25px] font-semibold text-fg leading-7 mb-4 px-1">
        Active Contracts
      </h2>
      
      {activeContracts.length > 0 ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 md:gap-6">
          {activeContracts.map((contract, index) => (
            <div 
              key={contract.id || index} 
              className="bg-surface rounded-xl p-5 md:p-6 border border-line-soft shadow-sm hover:shadow-md transition-shadow"
            >
              <div className="flex items-center gap-2 text-[11px] md:text-xs text-fg-subtle mb-2">
                <User className="w-3.5 h-3.5 md:w-4 md:h-4 text-fg-subtle" />
                <span className="truncate">{contract.client_name}</span>
              </div>

              <h3 className="text-sm md:text-[15px] font-bold text-fg leading-tight md:leading-[22px] mb-3 line-clamp-2 min-h-[2.5rem] md:min-h-0">
                {contract.project_title}
              </h3>

              <div className="flex items-center gap-2 text-[11px] md:text-xs text-fg-muted mb-4">
                <Calendar className="w-3.5 h-3.5 md:w-4 md:h-4 text-brand" />
                <span>
                  Due {getDueDate(contract.freelancer_signed_at, contract.delivery_days)}
                </span>
              </div>

              <div className="flex items-center justify-between pt-3 border-t border-line-soft">
                <div className="flex items-center gap-1.5">
                  <div className="p-1 bg-success-soft rounded">
                    <DollarSign className="w-3.5 h-3.5 md:w-4 md:h-4 text-success" />
                  </div>
                  <span className="text-xs md:text-sm font-bold text-fg">
                    ${contract.total_amount}
                  </span>
                </div>
                <Link 
                  to={`contracts/${contract.id}`} 
                  className="text-[11px] md:text-xs font-bold text-brand hover:text-brand transition-colors bg-brand-soft px-3 py-1.5 rounded-lg"
                >
                  Details →
                </Link>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="bg-surface rounded-xl p-8 md:p-12 border border-line-soft shadow-sm flex flex-col items-center justify-center text-center">
          <div className="w-14 h-14 bg-surface-2 rounded-full flex items-center justify-center mb-4">
            <FileX className="w-7 h-7 text-fg-subtle" />
          </div>
          <p className="text-fg-muted font-bold text-sm md:text-base">
            No active contracts found.
          </p>
          <p className="text-[11px] md:text-xs text-fg-subtle mt-1">
            Accept an offer to start a project.
          </p>
        </div>
      )}
    </div>
  )
}

export default ActiveContacts
