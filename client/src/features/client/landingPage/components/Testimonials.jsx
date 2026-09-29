export default function TestimonialsSection() {
  const testimonials = [
    {
      quote: "This platform transformed how we hire talent. The quality of freelancers is exceptional, and the process is incredibly smooth.",
      name: "Jane Doe",
      role: "CEO, TechCorp",
      avatar: "https://api.builder.io/api/v1/image/assets/TEMP/eb2ead37d5523f978b170dc3e3669a89ab5b05b2?width=80",
    },
    {
      quote: "Found the perfect UI/UX designer in just two days. The collaboration tools made the project a breeze. Highly recommended!",
      name: "John Smith",
      role: "Founder, Innovate Co.",
      avatar: "https://api.builder.io/api/v1/image/assets/TEMP/6792fa6edf95358eb4360faaf562940b8ec95683?width=80",
    },
    {
      quote: "Secure payments and milestone tracking gave me peace of mind. A trustworthy and professional service for any business.",
      name: "Emily Johnson",
      role: "Marketing Director, Creative Solutions",
      avatar: "https://api.builder.io/api/v1/image/assets/TEMP/8005a5800bb578d6276e603f10b01e03ff75f4c1?width=80",
    },
  ];

  return (
    <section className="py-20 px-4 md:px-8 bg-surface mb-20">
      <div className="max-w-page-3xl mx-auto">
        <h2 className="font-inter font-bold text-[40px] md:text-[51px] leading-5 tracking-[0.7px] uppercase text-fg text-center mb-16">
          What Our Clients Say
        </h2>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-8 md:gap-12 mt-30">
          {testimonials.map((testimonial, index) => (
            <div
              key={index}
              className="flex flex-col justify-between p-8 rounded-3xl border border-line bg-surface hover:shadow-lg transition-shadow"
            >
              <p className="font-grotesk font-bold text-base leading-6 text-fg-muted mb-8">
                "{testimonial.quote}"
              </p>

              <div className="flex items-center gap-4">
                <img
                  src={testimonial.avatar}
                  alt={testimonial.name}
                  className="w-10 h-10 rounded-full object-cover"
                />
                <div>
                  <h4 className="font-grotesk font-bold text-base leading-6 text-fg">
                    {testimonial.name}
                  </h4>
                  <p className="font-grotesk font-bold text-xs leading-4 text-fg-muted">
                    {testimonial.role}
                  </p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
