import { Bell, Mail, Menu } from 'lucide-react';
import { Link } from 'react-router-dom';

export function FreelancerHeader({ onMenuClick }) {
  return (
    <header className="w-full h-[70px] md:h-[80px] bg-white border-b border-[#F3F4F6] flex items-center justify-between px-4 md:px-10 sticky top-0 z-30">
      <div className="flex items-center gap-3">
        <button onClick={onMenuClick} className="lg:hidden p-2 hover:bg-gray-100 rounded-lg transition-colors">
          <Menu className="w-6 h-6 text-gray-600" />
        </button>

        <Link to="/" className="flex items-center">
          <h1 className="text-[24px] md:text-[40px] leading-none font-[900] tracking-tight" style={{ fontFamily: 'MuseoModerno, sans-serif' }}>
            <span className="text-[#1A1A1A]">Gro</span>
            <span className="text-primary">lance</span>
          </h1>
        </Link>
      </div>

      <div className="flex items-center gap-2 md:gap-6">
        <button className="flex items-center gap-2 h-9 md:h-11 px-3 md:px-5 rounded-xl border border-blue-100 bg-blue-50/30 hover:bg-blue-50 transition-colors">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
            <path d="M5.33334 2L2.66667 4.66667L5.33334 7.33333" stroke="#3B82F6" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            <path d="M2.66667 4.6665H13.3333" stroke="#3B82F6" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            <path d="M10.6667 13.9998L13.3333 11.3332L10.6667 8.6665" stroke="#3B82F6" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
            <path d="M13.3333 11.3335H2.66667" stroke="#3B82F6" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
          <span className="hidden sm:inline text-xs md:text-sm font-bold text-[#3B82F6]">Switch to Client</span>
        </button>

        <div className="flex items-center gap-1 md:gap-2">
          <button className="relative w-9 h-9 md:w-11 md:h-11 rounded-full flex items-center justify-center hover:bg-gray-100">
            <Mail className="w-5 h-5 md:w-6 md:h-6 text-[#4B5563]" />
          </button>

          <button className="relative w-9 h-9 md:w-11 md:h-11 flex items-center justify-center hover:bg-gray-100 rounded-full">
            <Bell className="w-5 h-5 md:w-6 md:h-6 text-[#4B5563]" />
          </button>
        </div>
      </div>
    </header>
  );
}

export default FreelancerHeader;
