import {useSelector} from "react-redux";
export default function AdminHeader() {
  const user = useSelector(state=>state.auth.user)
  
  return (

    <header className="h-20 bg-surface border-b border-line-soft flex items-center justify-between px-8">
      
      <h1 className="text-xl font-semibold text-fg tracking-tight">
        Admin Dashboard
      </h1>

      <div className="flex items-center gap-5">
        <div className="border-l border-line pl-5">
          <div className="text-right leading-tight">
            <p className="text-sm font-semibold text-fg">
              {user?.full_name}
            </p>
            <p className="text-xs text-fg-subtle">
              Super Admin
            </p>
          </div>
        </div>
      </div>
    </header>
  );
}
