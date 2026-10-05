import React from "react";
import { ShieldCheck, Zap, Users, Landmark, Scale, CheckCircle2 } from "lucide-react";
import { Link } from "react-router-dom";
import Footer from "../../features/client/landingPage/components/Footer";
import { useSelector } from "react-redux";
import ClientHeader from "../../features/client/homepage/components/ClientHeader";
import { FreelancerHeader } from "../../features/freelancer/dashboard/components/Header";
import Header from "../../features/client/landingPage/components/Header";
import ClientFooter from "../../features/client/homepage/components/ClientFooter";
import FreelancerFooter from "../../features/freelancer/dashboard/components/FreelancerFooter";


export default function AboutPage() {
  const user = useSelector(state=>state.auth.user)
  
  return (
    <div className="min-h-screen bg-surface-2">
      {user ? 
        (user?.current_role === 'client' ? <ClientHeader /> : <div className='fixed w-full z-100'><FreelancerHeader/></div> ) 
        : <Header />
      }

      <section className="py-25 px-6">
        <div className="max-w-4xl mx-auto text-center">
          <span className="text-[10px] font-bold text-brand uppercase tracking-[0.3em] mb-4 block">Our Purpose</span>
          <h1 className="text-4xl md:text-6xl font-extrabold text-fg tracking-tight mb-8">
            Redefining the economy of <span className="text-brand">digital work.</span>
          </h1>
          <p className="text-lg text-fg-muted leading-relaxed mb-10">
            We built this platform to bridge the trust gap between elite freelancers and global clients. 
            By combining legal-grade service agreements with secure escrow technology, we ensure every project 
            is a guaranteed success.
          </p>
        </div>
      </section>

      <section className="pb-24 px-6">
        <div className="max-w-7xl mx-auto grid grid-cols-1 md:grid-cols-3 gap-8">
          <div className="bg-surface p-10 rounded-2xl border border-line shadow-sm">
            <div className="w-12 h-12 bg-brand-soft rounded-xl flex items-center justify-center mb-6">
              <ShieldCheck className="w-6 h-6 text-brand" />
            </div>
            <h3 className="text-lg font-bold text-fg mb-4">Escrow Protection</h3>
            <p className="text-sm text-fg-subtle leading-relaxed font-medium">
              Funds are held securely by our platform and disbursed only when you approve the deliverables. 
              Zero risk, total transparency.
            </p>
          </div>
          <div className="bg-surface p-10 rounded-2xl border border-line shadow-sm">
            <div className="w-12 h-12 bg-warning-soft rounded-xl flex items-center justify-center mb-6">
              <Scale className="w-6 h-6 text-warning" />
            </div>
            <h3 className="text-lg font-bold text-fg mb-4">Arbitration Excellence</h3>
            <p className="text-sm text-fg-subtle leading-relaxed font-medium">
              Our Resolution Center Audit ensures fair treatment. If a dispute arises, our unbiased 
              arbitrators review the contract specifications to provide a just outcome.
            </p>
          </div>
          <div className="bg-surface p-10 rounded-2xl border border-line shadow-sm">
            <div className="w-12 h-12 bg-warning-soft rounded-xl flex items-center justify-center mb-6">
              <Landmark className="w-6 h-6 text-warning-soft-fg" />
            </div>
            <h3 className="text-lg font-bold text-fg mb-4">Legal Compliance</h3>
            <p className="text-sm text-fg-subtle leading-relaxed font-medium">
              Every order generates a legally binding Service Agreement, protecting the intellectual 
              property and commercial rights of both parties.
            </p>
          </div>
        </div>
      </section>

      <section className="bg-inverse py-24 px-6 rounded-[2rem] mx-4 mb-24 overflow-hidden relative">
        <div className="max-w-7xl mx-auto relative z-10">
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-12 text-center">
            <div>
              <p className="text-4xl font-black text-white mb-2 tracking-tighter">$2M+</p>
              <p className="text-xs font-bold text-fg-subtle uppercase tracking-widest">Escrowed Funds</p>
            </div>
            <div>
              <p className="text-4xl font-black text-white mb-2 tracking-tighter">99.2%</p>
              <p className="text-xs font-bold text-fg-subtle uppercase tracking-widest">Project Success Rate</p>
            </div>
            <div>
              <p className="text-4xl font-black text-white mb-2 tracking-tighter">24H</p>
              <p className="text-xs font-bold text-fg-subtle uppercase tracking-widest">Avg. Dispute Resolution</p>
            </div>
            <div>
              <p className="text-4xl font-black text-white mb-2 tracking-tighter">15k+</p>
              <p className="text-xs font-bold text-fg-subtle uppercase tracking-widest">Verified Professionals</p>
            </div>
          </div>
        </div>
      </section>

     
     {user ? 
        (user?.current_role === 'client' ? <ClientFooter/> : <div><FreelancerFooter/></div> ) 
        : <Footer />
      }
    </div>
  );
}