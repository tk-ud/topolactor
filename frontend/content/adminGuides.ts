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
  title: "管理画面の使い方",
  purpose:
    `${UX_CONTENTS} → ${UX_UI_BUILDER} → ${UX_HUB_MANIFESTS} の順で設定を進めます。`,
  prerequisites: ["先にログインしてください"],
  howToSteps: [
    `${UX_CONTENTS_PAGE}で下書きを作成し、内容確認・プレビュー・有効化を行う`,
    `${UX_UI_BUILDER}で部品を登録し、レイアウトを保存反映する`,
    `${UX_HUB_MANIFESTS_PAGE}で作成済みページの所属先、ページ間のつながり、表示順を管理する`,
    `必要に応じてデモ画面で表示と操作を確認する`,
  ],
  inputs: ["各画面で案内される設定内容"],
  actions: ["案内された順にページ作成、画面づくり、ページ間の導線設定を進める"],
  outputs: [
    "成功時: 保存結果が画面に表示されます",
    "失敗時: 修正方法を含むエラーが画面に表示されます",
  ],
  boundaryNotes: [
    "保存と検証はサーバーを経由します。直接 DB は編集しません。",
    "Preview は read-only。Apply / Promote は明示操作のみ。",
  ],
};

export const ADMIN_UI_BUILDER_GUIDE: AdminGuide = {
  title: UX_UI_BUILDER,
  purpose:
    "使いたい部品を登録し、ページ上の配置や見た目を整えて保存反映する画面です。",
  prerequisites: [
    "先にログインしてください",
    `${UX_CONTENTS_PAGE}でページ内容と${UX_DATA_SHAPE}を用意してください`,
  ],
  howToSteps: [
    "「部品登録」で使いたい部品を選び、配置できる状態にする",
    "「レイアウトビルダー」で対象ページとレイアウトを選ぶ",
    "キャンバスへ部品を配置し、必要に応じて見た目を調整する",
    "プレビュー → 内容確認 → 保存反映の順で進める",
    "まだ登録していない部品が残る場合は、先に登録するかキャンバスから外す",
  ],
  inputs: ["使用する部品、対象ページ、レイアウト、キャンバス上の配置"],
  actions: ["部品を登録する", "レイアウトを確認して保存反映する"],
  outputs: ["配置可能な部品", "保存反映されたレイアウト"],
  nextSteps: ["デモ画面で表示を確認する"],
  boundaryNotes: [
    "詳細設定では内部 ID や raw JSON を確認できます。",
    "直接 DB 編集は不可。",
  ],
  errorGuide: [
    "部品が表示されない場合は、部品登録を先に完了してください。",
    "保存反映できない場合は、未登録の部品を解消してください。",
  ],
  caution: "保存反映するとページのレイアウト設定が更新されます。",
};

export const ADMIN_CONTENTS_GUIDE: AdminGuide = {
  title: UX_CONTENTS,
  purpose:
    `ページの下書きを作成し、画面内容と${UX_DATA_SHAPE}を整え、内容確認・プレビュー後に有効化する画面です。ページ同士のつながりや表示順は ${UX_HUB_MANIFESTS_PAGE} で管理します。`,
  prerequisites: ["先にログインしてください"],
  howToSteps: [
    "新しいページの下書きを作成する",
    `ページの内容と${UX_DATA_SHAPE}を入力して保存する`,
    "内容確認 → プレビュー → 有効化の順で進める",
    `ページ同士のつながりや表示順は ${UX_HUB_MANIFESTS_PAGE} で管理する`,
  ],
  inputs: [
    `ページの内容と${UX_DATA_SHAPE}`,
    "表示・操作に必要なページ単体の設定",
  ],
  actions: ["下書き作成 / 設計保存 / 内容確認 / プレビュー / 有効化"],
  outputs: [
    "ページの下書き",
    "修正が必要な項目",
    "プレビュー結果",
    "有効化されたページ",
  ],
  nextSteps: ["/admin/ui-builder", "/admin/manifests"],
  boundaryNotes: [
    "直接 DB 編集は行いません。保存と検証はサーバーを経由します。",
    "ページ間の導線設定は扱いません。",
  ],
};

export const ADMIN_MANIFESTS_GUIDE: AdminGuide = {
  title: UX_HUB_MANIFESTS,
  purpose:
    `作成済みページの所属先、ページ間のつながり、ナビゲーションの表示順をまとめて管理する画面です。新しいページは ${UX_CONTENTS_PAGE} で作成します。`,
  prerequisites: [
    "先にログインしてください",
    `${UX_CONTENTS_PAGE}でページを作成してください`,
  ],
  howToSteps: [
    "作成済みページの一覧を確認する",
    "ページを選び、所属先とページ間のつながりを確認する",
    "必要な遷移先を追加・編集し、表示順を並び替える",
  ],
  inputs: ["作成済みページ、遷移先、表示順"],
  actions: ["一覧表示 / 遷移先の追加 / 編集 / 並び替え / 利用停止"],
  outputs: ["作成済みページ一覧", "ページ間の導線設定結果"],
  boundaryNotes: [
    "操作内容はサーバー側で検証します。",
    "接続できない場合はエラーを明示表示します。",
  ],
  caution: "保存するとページ間の導線設定が更新されます。",
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
    label: UX_CONTENTS,
    purpose: "新しいページの下書き作成・内容確認・プレビュー・有効化",
    relation: "Step 1",
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
    relation: "Step 2",
    howToSummary: [
      "部品を登録",
      "レイアウトを配置",
      "プレビュー → 検証 → 保存反映",
    ],
  },
  {
    href: "/admin/manifests",
    label: UX_HUB_MANIFESTS,
    purpose: "作成済みページの所属先、ページ間のつながり、表示順を管理する",
    relation: "Step 3",
    howToSummary: [
      "作成済みページを確認",
      "遷移先を追加・編集・並び替え",
    ],
  },
];

export const ADMIN_HUB_NAVIGATION_GUIDE: AdminGuide = {
  title: "ナビ順序設定",
  purpose: "設定単位のページ遷移順序を設定します。" +
    "画面間ナビゲーションの順序を管理する画面です。",
  prerequisites: [
    "/auth でログイン済みであること",
    "先にページを作成していること",
    `${UX_CONTENTS} で画面の内容が定義されていること`,
  ],
  howToSteps: [
    "「画面選択」で対象ページを選ぶ",
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
    nextLabel: `${UX_CONTENTS}へ`,
  },
  {
    step: 2,
    label: UX_CONTENTS,
    href: "/admin/contents",
    purpose: "新しいページの下書き・内容確認・プレビュー・有効化",
    completionSign: "新しいページが有効化されていること",
    nextLabel: `${UX_UI_BUILDER}へ`,
  },
  {
    step: 3,
    label: UX_UI_BUILDER,
    href: "/admin/ui-builder",
    purpose: "部品、配置、見た目を設定する",
    completionSign: "レイアウトの保存反映が完了していること",
    nextLabel: `${UX_HUB_MANIFESTS}へ`,
  },
  {
    step: 4,
    label: UX_HUB_MANIFESTS,
    href: "/admin/manifests",
    purpose: "作成済みページの所属先、ページ間のつながり、表示順を管理する",
    completionSign: "必要なページ間の導線設定が完了していること",
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
    label: UX_CONTENTS,
    href: "/admin/contents",
    checks: [
      "ドラフトを作成できること",
      "内容確認の前には有効化できないこと",
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
      "作成済みページ一覧が表示されること",
      "所属先、遷移先、表示順を同じ画面で設定できること",
    ],
  },
];
