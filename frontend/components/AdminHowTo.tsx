import { JSX } from "preact";

export type AdminHowToProps = {
  title?: string;
  steps: string[];
  prerequisites?: string[];
};

/** Always-visible numbered procedure (SSR-safe). */
export default function AdminHowTo({
  title = "操作手順（How to）",
  steps,
  prerequisites,
}: AdminHowToProps): JSX.Element {
  return (
    <section class="admin-how-to card mb-6">
      <h2 class="section-title mb-2">{title}</h2>
      {prerequisites && prerequisites.length > 0 && (
        <div class="alert-info mb-3 py-2 text-sm">
          <strong class="font-semibold">事前準備:</strong>
          <ul class="mt-1 list-inside list-disc">
            {prerequisites.map((p, i) => (
              <li key={i}>{p}</li>
            ))}
          </ul>
        </div>
      )}
      <ol class="list-decimal space-y-2 pl-5 text-sm leading-relaxed">
        {steps.map((step, i) => (
          <li key={i} class="pl-1">
            {step}
          </li>
        ))}
      </ol>
    </section>
  );
}
