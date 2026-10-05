import { z } from "zod";

export const projectCreateSchema = z
  .object({
    title: z
      .string()
      .min(5, 'Title must be greater than 5 characters')
      .max(255, 'Title must be less than 255 characters')
      // No character whitelist. The backend (ProjectValidators.cs) only enforces
      // length, so anything stricter rejected titles the server would have
      // accepted: "E-commerce Website Redesign" failed on the hyphen, and the
      // message wrongly referenced a "Full name". Real project titles contain
      // hyphens, ampersands, commas, # and parentheses.
      .refine((v) => v.trim().length > 0, "Title is required"),
    description: z.string().min(50,'Description must be greater than 50 characters').max(600,'Description must be less than 600 characters'),
    requirements: z.string().min(20,'Requirements must be greater than 20 characters').max(600,'Requirements must be less than 600 characters'),
    expected_deliverables: z.string().min(20,'Must be greater than 20 characters').max(600,'Must be less than 600 characters'),
    category: z.preprocess((val) => Number(val), z.number().min(1)),
    // Characters a real catalogue skill name is allowed to contain.
//
// This replaces two regexes that rejected 43 of the 654 seeded skills --
// including C#, C++, HTML5, CSS3, GPT-4, Llama 3, n8n and S3. Verified by
// running this schema over every catalogue name: C# and C++ are offered by this
// form's own suggestion chips and then refused by its own validation, and the
// drafting agent's suggestion for a responsive-website brief (CSS3) could not be
// posted.
//
// The intent behind the old rules was plainly to keep markup out of a string the
// server stores and renders, and that intent is kept: this is still an
// allow-list, so `<`, `>`, quotes and `&` remain rejected. It just stops
// rejecting legitimate technology names on the way.
//
// Digits are allowed because HTML5, CSS3, GPT-4 and S3 are not typos. The old
// message said "Title cannot contain numbers" on the *skills* field, which was a
// copy-paste from the title rule above.
skills: z
      .array(
        z
          .string()
          .trim()
          .min(1, "Skill cannot be blank")
          .regex(
            // A leading dot is allowed because `.NET`, `.NET Core` and `.NET MAUI`
            // are real catalogue entries. Requiring an alphanumeric first character
            // rejected those three; requiring *some* character is what actually
            // matters, and the allow-list is what keeps markup out.
            /^[A-Za-z0-9.][A-Za-z0-9 .+#/-]*$/,
            "A skill can contain letters, numbers, spaces and . + # / -"
          )
      )
      .min(3, "Pick at least 3 skills — they decide which freelancers see this project")
      
      ,
    pricing_type: z.enum(["fixed", "range"]),

    fixed_price: z.number().positive().nullish().optional(),
    min_budget: z.number().positive().nullish().optional(),
    max_budget: z.number().positive().nullish().optional(),

    delivery_days: z.number().int().positive().max(99),
  })
  .superRefine((data, ctx) => {
    if (data.pricing_type === "fixed") {
      if (data.fixed_price == null) {
        ctx.addIssue({
          path: ["fixed_price"],
          message: "Fixed price is required",
        });
      }
    }

    if (data.pricing_type === "range") {
      if (data.min_budget == null || data.max_budget == null) {
        ctx.addIssue({
          path: ["min_budget"],
          message: "Budget range is required",
        });
        return;
      }

      if (data.min_budget >= data.max_budget) {
        ctx.addIssue({
          path: ["max_budget"],
          message: "Max budget must be greater than min budget",
        });
      }
    }
  });
