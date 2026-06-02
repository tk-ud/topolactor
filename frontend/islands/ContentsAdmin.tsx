import { JSX } from "preact";
import AdminHowTo from "../components/AdminHowTo.tsx";
import AdminHelpPanel from "../components/AdminHelpPanel.tsx";
import { ADMIN_CONTENTS_GUIDE } from "../content/adminGuides.ts";
import { UX_HUB_MANIFESTS_PAGE } from "../content/adminUxTerms.ts";
import ContentsScreenDesignPanel from "./ContentsScreenDesignPanel.tsx";
import ContentsPromotionPanel from "./ContentsPromotionPanel.tsx";

/** Canonical single-page manifest authoring surface. */
export default function ContentsAdmin(): JSX.Element {
  return (
    <main class="page-main font-mono">
      <h1 class="page-title">topolactor — 管理 / 新規 manifest 作成</h1>
      <p class="mb-4"><a href="/admin" class="link">&larr; 管理インデックス</a></p>

      <section class="mb-4 rounded-lg border border-blue-200 bg-blue-50 p-4 text-sm text-blue-900">
        <p class="font-semibold">この画面の責務: 単独ページ manifest を作る</p>
        <p class="mt-1 text-xs">
          画面内容と data shape を定義し、単体ページとして下書き・内容確認・プレビュー・有効化を行います。
          作成済み manifest の hub 所属、manifest 間 relation、導線順序は
          <a href="/admin/manifests" class="link font-semibold"> {UX_HUB_MANIFESTS_PAGE}</a>
          で管理します。
        </p>
      </section>

      <AdminHowTo steps={ADMIN_CONTENTS_GUIDE.howToSteps} />
      <AdminHelpPanel {...ADMIN_CONTENTS_GUIDE} />

      <ContentsScreenDesignPanel />
      <ContentsPromotionPanel />
    </main>
  );
}
