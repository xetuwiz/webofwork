import { Loader2, ShieldCheck } from "lucide-react";
import {usePrivacyPolicies} from './privacyPolicyQueries'

export default function PrivacyPolicies() {
  const { data, isLoading } = usePrivacyPolicies();

  const scrollToSection = (id) => {
    const element = document.getElementById(id);
    if (element) {
      element.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  };

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-surface">
        <Loader2 className="w-8 h-8 animate-spin text-brand" />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-gray-50 via-gray-50 to-blue-50">
  
      <div className="w-full border-b border-line bg-surface">
        <div className="max-w-page-lg mx-auto px-6 py-8">
          <div className="flex items-center justify-between">
            <div className="flex flex-col gap-3">
              <h1 className="text-[31px] font-bold text-fg leading-[40px]">
                Privacy Policy
              </h1>
              <p className="text-[15px] text-fg-muted leading-[28px]">
                Your privacy is important to us. Learn how we protect and manage your data.
              </p>
            </div>
            <div className="flex items-center justify-center w-32 h-32 rounded-2xl bg-brand-soft">
                <ShieldCheck className="w-16 h-16 text-brand" />
            </div>
          </div>
        </div>
      </div>

      <div className="max-w-page-lg mx-auto px-6 py-12">
        <div className="flex flex-col lg:flex-row gap-12">
          <aside className="w-full lg:w-64 flex-shrink-0 lg:sticky lg:top-24 self-start">
            <div className="bg-surface border border-line-soft rounded-2xl shadow-sm p-6">
              <h2 className="text-[12px] font-semibold text-fg mb-4 uppercase tracking-wider">Policy Navigation</h2>
              <nav className="flex flex-col gap-2">
                {data?.results?.map((section) => (
                  <button
                    key={`nav-${section.id}`}
                    onClick={() => scrollToSection(`section-${section.id}`)}
                    className="text-left px-3 py-2 text-[12px] text-fg-muted hover:bg-brand-hover-soft hover:text-brand rounded-lg transition-all truncate"
                  >
                    {section.heading}
                  </button>
                ))}
              </nav>
            </div>
          </aside>

          <main id="main-content" tabIndex={-1} className="flex-1 pb-[52vh] flex flex-col gap-8">
            {data?.results?.map((section, index) => (
              <section 
                key={section.id} 
                id={`section-${section.id}`} 
                className="bg-surface border border-line-soft rounded-2xl shadow-sm p-8 scroll-mt-24 transition-all hover:shadow-md"
              >
                <div className="flex items-center gap-3 mb-4">
                    <span className="flex items-center justify-center w-8 h-8 rounded-full bg-brand-soft text-brand text-[14px] font-bold">
                        {index + 1}
                    </span>
                    <h2 className="text-[20px] font-semibold text-fg leading-[32px]">
                    {section.heading}
                    </h2>
                </div>
                <div className="space-y-4">
                  <div className="text-[14px] text-fg-muted leading-[26px] whitespace-pre-line prose prose-blue max-w-none">
                    {section.description}
                  </div>
                </div>
              </section>
            ))}
            
            {data?.results?.length === 0 && (
              <div className="text-center py-20 bg-surface rounded-2xl border border-dashed border-line-strong">
                <p className="text-fg-subtle font-medium">No privacy policy sections found.</p>
              </div>
            )}
          </main>
        </div>
      </div>
    </div>
  );
}
