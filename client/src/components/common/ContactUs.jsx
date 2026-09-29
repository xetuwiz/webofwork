import { Mail, MapPin, Phone, Clock, ShieldCheck } from 'lucide-react';
import Footer from '../../features/client/landingPage/components/Footer';
import { useSelector } from 'react-redux';
import ClientHeader from '../../features/client/homepage/components/ClientHeader';
import { FreelancerHeader } from '../../features/freelancer/dashboard/components/Header';
import Header from '../../features/client/landingPage/components/Header';
import ClientFooter from '../../features/client/homepage/components/ClientFooter';
import FreelancerFooter from '../../features/freelancer/dashboard/components/FreelancerFooter';


const ContactPage = () => {
  const emailAddress = "support@yourdomain.com";
  const officeAddress = "123 Business Avenue, Suite 500, New York, NY 10001";
  const phoneNumber = "+1 (555) 000-0000";
  const user = useSelector(state=>state.auth.user)

  return (
    <div className="min-h-screen bg-surface flex flex-col font-sans selection:bg-surface-inset">
      
      {user?
      user.current_role === 'client' ? <ClientHeader/> : <FreelancerHeader/> : <Header/>}
      <main id="main-content" tabIndex={-1} className="flex-grow">
        <div className="bg-surface border-b border-line-soft">
          <div className="max-w-7xl mx-auto pt-32 pb-20 lg:pt-52 lg:pb-32 px-6 text-center">
            
            <h1 className="text-4xl lg:text-6xl font-extrabold text-fg tracking-tight mb-8 leading-[1.1]">
              Get in touch with our team 
            </h1>
            <p className="text-lg text-fg-subtle max-w-2xl mx-auto leading-relaxed font-medium">
              Connect with our specialized team through our established 
              corporate channels for reliable, high-tier assistance.
            </p>
          </div>
        </div>

        <div className="max-w-7xl mx-auto px-6 -mt-16 pb-28">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-24">
            <div className="bg-surface p-10 rounded-[2rem] border border-line shadow-[0_8px_30px_rgb(0,0,0,0.04)] flex flex-col items-center text-center transition-all duration-slow hover:border-line-strong hover:shadow-lg">
              <div className="w-16 h-16 bg-brand text-brand-fg rounded-2xl flex items-center justify-center mb-8 shadow-xl shadow-slate-200">
                <Mail className="w-7 h-7" />
              </div>
              <h3 className="text-lg font-bold text-fg mb-3">Electronic Mail</h3>
              <p className="text-fg-subtle mb-8 text-sm leading-relaxed font-medium">For technical documentation <br/>and general inquiries.</p>
              <a 
                href={`mailto:${emailAddress}`}
                className="w-full py-4 bg-surface-2 text-fg border border-line rounded-xl font-bold text-sm flex items-center justify-center gap-2 hover:bg-brand-hover hover:text-brand-fg transition-all duration-slow"
              >
                {emailAddress}
              </a>
            </div>

            <div className="bg-surface p-10 rounded-[2rem] border border-line shadow-[0_8px_30px_rgb(0,0,0,0.04)] flex flex-col items-center text-center transition-all duration-slow hover:border-line-strong hover:shadow-lg">
              <div className="w-16 h-16 bg-surface border border-line text-fg rounded-2xl flex items-center justify-center mb-8">
                <MapPin className="w-7 h-7 text-brand" />
              </div>
              <h3 className="text-lg font-bold text-fg mb-3">Global Headquarters</h3>
              <p className="text-fg-subtle mb-8 text-sm leading-relaxed font-medium">Strategic operations and <br/>scheduled consultations.</p>
              <address className="not-italic text-fg font-bold text-sm leading-relaxed bg-surface-2 w-full py-4 rounded-xl border border-line-soft px-4">
                {officeAddress}
              </address>
            </div>

            <div className="bg-surface p-10 rounded-[2rem] border border-line shadow-[0_8px_30px_rgb(0,0,0,0.04)] flex flex-col items-center text-center transition-all duration-slow hover:border-line-strong hover:shadow-lg">
              <div className="w-16 h-16 bg-surface-3 text-fg rounded-2xl flex items-center justify-center mb-8">
                <Phone className="w-7 h-7 text-brand" />
              </div>
              <h3 className="text-lg font-bold text-fg mb-3">Direct Line</h3>
              <p className="text-fg-subtle mb-8 text-sm leading-relaxed font-medium">Live support available <br/>during standard EST hours.</p>
              <a 
                href={`tel:${phoneNumber}`}
                className="w-full py-4 bg-brand text-brand-fg rounded-xl font-bold text-sm flex items-center justify-center gap-2 hover:bg-inverse transition-all shadow-lg shadow-slate-100"
              >
                {phoneNumber}
              </a>
            </div>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-20 items-start border-t border-line-soft pt-24">
            <div className="space-y-12">
              <div>
                <h2 className="text-3xl font-extrabold text-fg mb-6 tracking-tight">Support Documentation</h2>
                <p className="text-fg-subtle leading-relaxed font-medium">Review our most frequently requested information for immediate clarity.</p>
              </div>
              
              <div className="space-y-10">
                {[
                  { q: "Standard SLA Response", a: "Formal inquiries are addressed within one business day via our ticketing protocol." },
                  { q: "Priority Account Management", a: "Enterprise partners maintain 24/7 access through encrypted dedicated channels." },
                  { q: "Governance & Appointments", a: "On-site visits are managed through our corporate scheduling department." }
                ].map((faq, i) => (
                  <div key={i} className="flex gap-6">
                    <div className="mt-1 shrink-0 w-10 h-10 rounded-xl bg-surface-2 border border-line flex items-center justify-center">
                      <Clock className="w-5 h-5 text-fg-subtle" />
                    </div>
                    <div>
                      <h4 className="font-bold text-fg mb-2">{faq.q}</h4>
                      <p className="text-sm text-fg-subtle leading-relaxed font-medium">{faq.a}</p>
                    </div>
                  </div>
                ))}
              </div>
            </div>

            <div className="bg-brand p-12 rounded-[3rem] text-brand-fg relative overflow-hidden">
              <div className="relative z-10">
                <div className="w-14 h-14 bg-surface/5 border border-white/10 rounded-2xl flex items-center justify-center mb-10">
                  <ShieldCheck className="w-7 h-7 " />
                </div>
                <h3 className="text-2xl font-bold mb-6 tracking-tight">Security Protocol</h3>
                <p className=" leading-relaxed mb-10 font-medium">
                  To maintain the integrity of your account, verify that all correspondence originates from our verified domain. Our security team will never request confidential credentials or sensitive financial keys.
                </p>
                
              </div>
              <div className="absolute -top-24 -right-24 w-80 h-80 bg-brand-soft/5 rounded-full blur-[100px]"></div>
            </div>
          </div>
        </div>
      </main>

      {user?
      user.current_role === 'client' ? <ClientFooter/> : <FreelancerFooter/> : <Footer/>}
    </div>
  );
};

export default ContactPage;
