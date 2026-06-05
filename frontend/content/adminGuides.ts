/** Static copy for /admin help panels — aligned with docs/design/admin-console-workflow-ssot.yaml v0.8.0 */

import {
  UX_CONTENTS,
  UX_CONTENTS_PAGE,
  UX_DATA_SHAPE,
  UX_HUB_MANIFESTS,
  UX_HUB_MANIFESTS_PAGE,
  UX_ENUM_ROSTER,
  UX_USER_ROSTER,
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
    `${UX_CONTENTS_PAGE}で step 1→3（空登録・テーブル・関連・ページ設定）を順に保存する`,
    `${UX_UI_BUILDER}で step 4（部品選択でパッケージ化 → 配置・デザイン設定・保存反映）を行う`,
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
    "部品を複数選択し、1 回でパッケージ化する（step 4.1）",
    "パッケージを選んで canvas workspace で配置を編集し、右パネルのデザインインスペクタで cssTokenRefs・色・形を設定する",
    "プレビュー → 検証 → 保存反映の順で layout を確定する",
    "デモ画面で動作を確認する",
  ],
  inputs: ["選択する部品、パッケージ、配置とデザインの意図"],
  actions: [
    "部品選択でパッケージ化（step 4.1）",
    "パッケージの配置・デザイン設定・配線を保存（step 4.2）",
    "配置: プレビュー → 検証 → 保存反映（パッケージ選択必須）",
  ],
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
    `ページの下書きを作成し、画面内容と${UX_DATA_SHAPE}を整え、プレビューして保存する画面です。ページ同士のつながりや表示順は ${UX_HUB_MANIFESTS_PAGE} で管理します。`,
  prerequisites: ["先にログインしてください"],
  howToSteps: [
    "Step 1: 空の下書きとトポロジー表示名を登録する",
    "Step 2: テーブル定義（項目名と型）を保存する",
    "Step 2.5: テーブル間の関連を保存する（任意）",
    "Step 3: ページ名・操作種別（複数可）・検索・集計・データ入力を保存する",
    `完了後は ${UX_UI_BUILDER} へ。ページ間のつながりは ${UX_HUB_MANIFESTS_PAGE}`,
  ],
  inputs: [
    `ページの内容と${UX_DATA_SHAPE}`,
    "表示・操作に必要なページ単体の設定",
  ],
  actions: ["Step 1–3 の保存"],
  outputs: [
    "ページの下書き",
    "修正が必要な項目",
    "プレビュー結果",
    "保存済みのページ設定",
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
    purpose: "step 1–3 を順に保存（空登録 → テーブル → 関連 → ページ設定）",
    relation: "作業順の最初の 3 ステップ",
    howToSummary: [
      "空登録 → テーブル定義 → 関連 → 物理割当",
      "保存後は画面づくりへ",
    ],
  },
  {
    href: "/admin/ui-builder",
    label: UX_UI_BUILDER,
    purpose: "部品選択でパッケージ化 → canvas workspace で配置・デザインを統合編集",
    relation: "作業順 step 4",
    howToSummary: [
      "部品を複数選択してパッケージ化",
      "配置とデザイン設定を編集して保存反映",
    ],
  },
  {
    href: "/admin/manifests",
    label: UX_HUB_MANIFESTS,
    purpose: "作成済みページの所属先、ページ間のつながり、表示順を管理する",
    relation: "作業順の最終段階（ページ同士をつなぐ）",
    howToSummary: [
      "作成済みページを確認",
      "遷移先を追加・編集・並び替え",
    ],
  },
  {
    href: "/admin/enums",
    label: UX_ENUM_ROSTER,
    purpose: "enum グループ・項目の名簿管理",
    relation: "master-roster",
    howToSummary: [
      "検索または全件出力で一覧を確認",
      "新規追加はモーダル、更新は行クリック後のインライン編集",
      "削除は確認ダイアログ必須",
    ],
  },
  {
    href: "/admin/users",
    label: UX_USER_ROSTER,
    purpose: "ユーザー名簿と承認・状態・停止期間の管理",
    relation: "master-roster",
    howToSummary: [
      "status は seed 済み enum から select",
      "最終ログインは参照のみ",
      "削除は確認ダイアログ必須",
    ],
  },
];

export const ADMIN_HUB_NAVIGATION_GUIDE: AdminGuide = {
  title: "ナビ順序設定",
  purpose: `${UX_HUB_MANIFESTS_PAGE} 内で、作成済みページの遷移順序を設定します。`,
  prerequisites: [
    "/super_auth で管理ログイン済みであること",
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
    label: UX_CONTENTS,
    href: "/admin/contents",
    purpose: "step 1 ～ step 3（空登録・テーブル・関連・ページ設定）を順に保存",
    completionSign: "step 3 まで保存済みであること",
    nextLabel: `${UX_UI_BUILDER}へ`,
    subSteps: [
      { label: "1 空登録", href: "/admin/contents" },
      { label: "2 テーブル", href: "/admin/contents" },
      { label: "2.5 関連", href: "/admin/contents" },
      { label: "3 ページ", href: "/admin/contents" },
    ],
  },
  {
    step: 2,
    label: UX_UI_BUILDER,
    href: "/admin/ui-builder",
    purpose: "部品選択でパッケージ化 → canvas workspace で配置・デザインを統合編集",
    completionSign: "配置の保存反映が完了していること",
    nextLabel: `${UX_HUB_MANIFESTS}へ`,
  },
  {
    step: 3,
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
      "step 1–3 を順に保存できること",
      "操作種別を複数選択できること",
      "保存後に画面づくりへ進めること",
    ],
  },
  {
    label: UX_UI_BUILDER,
    href: "/admin/ui-builder",
    checks: [
      "部品を複数選択してパッケージ化できること",
      "パッケージ選択後に canvas workspace で配置・デザインを統合編集できること",
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
