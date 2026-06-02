/** Static copy for /admin help panels — aligned with docs/registrar-admin-ui-specification.md */

import {
  UX_CONTENTS,
  UX_CONTENTS_PAGE,
  UX_DATA_SHAPE,
  UX_HUB_MANIFESTS,
  UX_HUB_MANIFESTS_PAGE,
  UX_RUNTIME_CHECK,
  UX_UI_BUILDER,
} from "./adminUxTerms.ts";

export type AdminGuide = {
  title: string;
  purpose: string;
  howToSteps: string[];
  prerequisites?: string[];
  inputs: string[];
  actions: string[];
  outputs: string[];
  nextSteps?: string[];
  boundaryNotes?: string[];
  errorGuide?: string[];
  caution?: string;
};

export const ADMIN_INDEX_GUIDE: AdminGuide = {
  title: "管理 UI（入口）",
  purpose:
    `${UX_CONTENTS} → ${UX_UI_BUILDER} → ${UX_HUB_MANIFESTS} の canonical admin workflow を案内します。直接 DB を編集する画面ではありません。`,
  prerequisites: [
    "/auth でログイン（管理画面利用の前提）",
    "DB 永続化する場合: infra 起動と DATABASE_URL / DEMO_BACKEND_URL の設定",
  ],
  howToSteps: [
    `/admin/contents で新規 manifest の下書きを作成し、内容確認・プレビュー・有効化を行う`,
    "/admin/ui-builder で部品登録・レイアウトを準備し、保存反映する",
    `/admin/manifests で作成済み manifest の hub 所属、relation、導線順序を管理する`,
    "動作確認が必要な場合は canonical admin workflow 外の /demo を利用する",
  ],
  inputs: [
    "各 canonical admin 画面で説明される登録対象",
    "操作前に /auth でログイン",
  ],
  actions: [
    "canonical route registry に沿ってページ作成、UI 準備、ページ同士の導線管理を進める",
  ],
  outputs: [
    "成功時: backend が検証したうえで canonical topology に反映",
    "失敗時: 画面のエラーパネルに code / message — silent fallback はしません",
  ],
  boundaryNotes: [
    "Frontend = projection / intent submission。意味判断・永続化の正本は DB + backend runtime",
    "Preview は read-only。Apply / Promote は明示操作のみ",
  ],
};

export const ADMIN_UI_BUILDER_GUIDE: AdminGuide = {
  title: "UI ビルダー（コンポーネント & レイアウト）",
  purpose:
    "プリミティブコンポーネントを UI topology DB に登録し、layout patch でグリッド配置を draft → validate → apply する画面です。",
  prerequisites: [
    "/auth 済み、UI topology DB 接続可能",
    `${UX_CONTENTS}で画面の内容と${UX_DATA_SHAPE}が定義されていること`,
    "データは /admin/contents で登録済みであること（推奨）",
    "componentKey / sourcePath は frontend/components の実在パスと一致させる",
  ],
  howToSteps: [
    "タブ「バケット」: componentKey・sourcePath・componentKind を入力し「バケットに登録」→「バケット再ロード」で一覧確認",
    "バケット行を選択し routeKey（既存または新規）を指定 →「生成」で packaging →「プロモート」で DB に componentId/packageId/layoutId/wiringId を発行",
    "タブ「レイアウト」: 「プロモート済みパレット」をロードし、キャンバスへドラッグ（ドラフトのみ＝オレンジは apply 不可）",
    "layoutId / routeKey を選び、必要なら topology layout class を指定",
    "「1. プレビュー」で解決結果のみ確認（DB 不変）→「2. バリデート」で ref 整合 → 問題なければ「3. 適用」",
    "ドラフトのみノードが残っていると適用はブロック — プロモートするかキャンバスから削除",
    "（任意）タブ「CI」で break_boundary ガイダンスを確認してから promote/apply",
  ],
  inputs: [
    "バケット候補、routeKey、layoutId、グリッド上のノード配置",
  ],
  actions: [
    "bucket → generate → promote",
    "layout patch: preview → validate → apply",
  ],
  outputs: [
    "プロモート: 発行 ID と promoted パレット",
    "apply: UI topology DB に layout 永続",
  ],
  nextSteps: [
    "/demo で投影確認",
    "推薦連携は Context Token Registry",
  ],
  boundaryNotes: [
    "code-only = drift。promote 完了まで topology エンティティではない",
    "直接 DB 編集は不可",
  ],
  errorGuide: [
    "パレット空 → バケット登録とプロモートを先に",
    "apply blocked → draft-only ノードを解消",
  ],
  caution: "プロモートと layout の「適用」は DB 書き込みです。",
};

export const ADMIN_CONTENTS_GUIDE: AdminGuide = {
  title: "新規 manifest 作成",
  purpose:
    `単独ページ manifest の下書きを作成し、画面内容と${UX_DATA_SHAPE}を整え、内容確認・プレビュー後に明示的に有効化する画面です。hub 所属、manifest 間 relation、導線順序は ${UX_HUB_MANIFESTS_PAGE} で管理します。`,
  prerequisites: [
    "/auth 済み、DATABASE_URL / DEMO_BACKEND_URL 設定済み",
  ],
  howToSteps: [
    "新規 manifest の下書きを作成する",
    `単独ページの画面内容と${UX_DATA_SHAPE}を入力して保存する`,
    "内容確認 → プレビュー → 有効化の順で進める",
    `hub 所属、manifest 間 relation、導線順序は /admin/manifests で管理する`,
  ],
  inputs: [
    `単独ページの画面内容と${UX_DATA_SHAPE}`,
    "表示・操作に必要なページ単体の設定",
  ],
  actions: [
    "manifest draft 作成 / 設計保存 / validate / preview / promote",
  ],
  outputs: [
    "単独ページ manifest draft",
    "validation issues（blocking / warning）",
    "preview summary",
    "有効化された manifest",
  ],
  nextSteps: [
    "/admin/ui-builder",
    "/admin/manifests",
  ],
  boundaryNotes: [
    "direct DB 編集は行いません — backend validation / persistence boundary 経由のみ",
    "hub 所属、manifest 間 relation、導線順序は扱いません",
    "frontend は topology 意味判断をしません",
  ],
};

export const ADMIN_MANIFESTS_GUIDE: AdminGuide = {
  title: UX_HUB_MANIFESTS,
  purpose:
    `作成済み manifest の hub 所属、manifest 間 relation、導線順序をまとめて管理する画面です。画面群としての連続性を ${UX_HUB_MANIFESTS_PAGE} で扱います。新規 manifest 作成は ${UX_CONTENTS_PAGE} で行います。`,
  prerequisites: [
    "/auth でログイン済みであること",
    `${UX_CONTENTS} で新規 manifest が作成済みであること`,
  ],
  howToSteps: [
    "作成済み topology_manifest 一覧を確認する",
    "manifest の hub 所属を確認・管理する",
    "同じ画面内で manifest 間 relation と導線順序を追加・編集・並び替える",
  ],
  inputs: [
    "作成済み topology_manifest の hub membership / relation / navigation ordering intent",
  ],
  actions: [
    "作成済み topology_manifest 一覧表示",
    "hub 所属管理 / relation 追加 / 編集 / 並び替え / 利用停止",
  ],
  outputs: [
    "作成済み topology_manifest 一覧",
    "hub 所属 / relation / navigation ordering 操作結果",
  ],
  boundaryNotes: [
    "Frontend は操作送信のみ — 内容の正しさはサーバー側で検証",
    "silent fallback 禁止 — バックエンド未接続は明示表示",
  ],
  caution: "hub 所属 / relation / navigation ordering 操作は DB の状態を変更します。",
};

/** Index page: per-route cards with short how-to */
export const ADMIN_ROUTE_CARDS: {
  href: string;
  label: string;
  purpose: string;
  relation: string;
  howToSummary: string[];
  caution?: string;
}[] = [
  {
    href: "/admin/contents",
    label: "新規 manifest 作成",
    purpose: "新しい manifest のドラフト作成・内容確認・プレビュー・有効化",
    relation: "canonical workflow Step 1",
    howToSummary: [
      "新規ドラフトを作成",
      "内容確認 → プレビュー",
      "明示的に有効化",
    ],
  },
  {
    href: "/admin/ui-builder",
    label: UX_UI_BUILDER,
    purpose: "画面部品の登録とレイアウトの準備",
    relation: "canonical workflow Step 2",
    howToSummary: [
      "部品を登録",
      "レイアウトを配置",
      "プレビュー → 検証 → 保存反映",
    ],
  },
  {
    href: "/admin/manifests",
    label: UX_HUB_MANIFESTS,
    purpose: "作成済み manifest の hub 所属、relation、導線順序管理",
    relation: "canonical workflow Step 3",
    howToSummary: [
      "既存 manifest を確認",
      "relation を追加・編集・並び替え",
    ],
  },
];

export const ADMIN_HUB_NAVIGATION_GUIDE: AdminGuide = {
  title: "ナビ順序設定",
  purpose: "設定単位のページ遷移順序を設定します。" +
    "画面間ナビゲーションの順序を管理する画面です。",
  prerequisites: [
    "/auth でログイン済みであること",
    "先に /admin/manifests で画面群（topology_manifest）が登録されていること",
    `${UX_CONTENTS} で画面の内容が定義されていること`,
  ],
  howToSteps: [
    "「画面選択」で対象の topology_manifest を選ぶ",
    "ナビ遷移が未登録なら「追加」フォームが自動表示される",
    "遷移先の画面を選んで「登録」（順序は自動で末尾に追加されます）",
    "既存エントリの「編集」でナビ遷移先を変更できる",
    "順序の変更は▲▼ボタンで行う",
    "不要なエントリは「削除」で無効化する（記録は保持）",
  ],
  inputs: [
    "設定 — 一覧から選択",
    "遷移先の画面 — 一覧から選択",
    "順序 — 末尾に自動追加（▲▼で変更可能）",
  ],
  actions: [
    "追加 — ナビ遷移を新規追加（有効）",
    "編集 — 遷移先を変更",
    "▲▼ — 順序を並び替え",
    "削除 — ナビ遷移を無効化（記録は保持）",
  ],
  outputs: [
    "成功時: ナビ遷移が登録され一覧に反映される",
    "失敗時: 競合・設定未発見などのエラーが表示される",
  ],
  boundaryNotes: [
    "Frontend は操作送信のみ。書き込みはサーバー側で管理される",
    "順序番号は設定スコープで一意です — 重複は自動回避されます",
  ],
};

/** 推奨受入フロー — admin index の "推奨受入フロー" セクションで使用 */
export type AcceptanceFlowStep = {
  step: number;
  label: string;
  href: string;
  purpose: string;
  completionSign: string;
  nextLabel?: string;
  boundaryNote?: string;
  subSteps?: { label: string; href: string }[];
};

/** 管理トップのコンパクトステッパー — admin-console-workflow-ssot.yaml canonical_workflow 準拠 */
export const ADMIN_MAIN_FLOW_STEPS: AcceptanceFlowStep[] = [
  {
    step: 1,
    label: "ログイン",
    href: "/auth",
    purpose: "管理画面利用の前提",
    completionSign: "ログイン済みであること",
    nextLabel: "新規 manifest 作成へ",
  },
  {
    step: 2,
    label: "新規 manifest 作成",
    href: "/admin/contents",
    purpose: "新しい manifest の下書き・内容確認・プレビュー・有効化",
    completionSign: "新規 manifest が有効化されていること",
    nextLabel: `${UX_UI_BUILDER}へ`,
  },
  {
    step: 3,
    label: UX_UI_BUILDER,
    href: "/admin/ui-builder",
    purpose: "表示・操作 UI（component / layout / style）",
    completionSign: "レイアウトの保存反映が完了していること",
    nextLabel: `${UX_HUB_MANIFESTS}へ`,
  },
  {
    step: 4,
    label: UX_HUB_MANIFESTS,
    href: "/admin/manifests",
    purpose: "作成済み manifest の hub 所属、relation、導線順序管理",
    completionSign: "必要な hub 所属、relation、導線順序操作が完了していること",
  },
];

export const ACCEPTANCE_FLOW_STEPS: AcceptanceFlowStep[] = ADMIN_MAIN_FLOW_STEPS
  .map((step) => ({ ...step }));

/** 受入チェックリスト — 完了判定の正本ではなく確認観点のリスト */
export type AcceptanceCheckItem = {
  label: string;
  href: string;
  checks: string[];
};

export const ACCEPTANCE_CHECKLIST: AcceptanceCheckItem[] = [
  {
    label: "新規 manifest 作成",
    href: "/admin/contents",
    checks: [
      "ドラフトを作成できること",
      "validation 未実行では有効化できないこと",
      "内容確認 → プレビュー → 有効化ができること",
    ],
  },
  {
    label: UX_UI_BUILDER,
    href: "/admin/ui-builder",
    checks: [
      "部品を登録できること",
      "レイアウトのプレビュー・検証・保存反映ができること",
    ],
  },
  {
    label: UX_HUB_MANIFESTS,
    href: "/admin/manifests",
    checks: [
      "作成済み manifest 一覧が表示されること",
      "hub 所属、relation、導線順序操作を同じ画面で行えること",
    ],
  },
];
