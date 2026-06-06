import { JSX } from "preact";
import type { RecommendNavigationProjectionSpec } from "../api/dispatch.ts";
import { HubLocalRecommendationBlock } from "./HubLocalRecommendationBlock.tsx";
import { SqlAttentionProjectionBlock } from "./SqlAttentionProjectionBlock.tsx";

export function RecommendNavigationIsland(
  { spec, token }: { spec: RecommendNavigationProjectionSpec; token?: string },
): JSX.Element {
  return (
    <aside
      class="mt-4 rounded-lg border border-blue-100 bg-blue-50 p-4"
      data-projection-island={spec.childIslandId}
      data-parent-projection-island={spec.mainProjectionIslandId}
    >
      <h3 class="mb-3 mt-0 text-base font-semibold">Recommend navigation child island</h3>
      <HubLocalRecommendationBlock sections={spec.hubLocalSections} />
      <div class="mt-3">
        <SqlAttentionProjectionBlock spec={spec.sqlAttentionProjection} token={token} />
      </div>
    </aside>
  );
}
