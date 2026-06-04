/** @jsxImportSource preact */
import { useEffect, useMemo, useState } from "preact/hooks";
import { JSX } from "preact";
import {
  createEnumDictionaryGroup,
  createEnumDictionaryItem,
  deleteEnumDictionaryGroup,
  deleteEnumDictionaryItem,
  getEnumDictionaryGroup,
  listEnumDictionaryGroups,
  setEnumDictionaryGroupItems,
  updateEnumDictionaryGroup,
  type EnumDictionaryGroup,
  type EnumDictionaryGroupDetail,
} from "../api/adminApi.ts";
import { Modal } from "../components/Modal.tsx";
import { ValidationErrorPanel } from "../components/ValidationErrorPanel.tsx";
import { useConfirm } from "../hooks/useConfirm.tsx";
import { runAdminSubmit } from "../lib/adminSubmitUx.ts";

type PanelError = { code?: string; message: string };

export default function AdminEnumsRoster(): JSX.Element {
  const [groups, setGroups] = useState<EnumDictionaryGroup[]>([]);
  const [search, setSearch] = useState("");
  const [selectedId, setSelectedId] = useState("");
  const [detail, setDetail] = useState<EnumDictionaryGroupDetail | null>(null);
  const [draftName, setDraftName] = useState("");
  const [draftIndexNums, setDraftIndexNums] = useState("");
  const [modalOpen, setModalOpen] = useState(false);
  const [newGroupName, setNewGroupName] = useState("");
  const [newItemName, setNewItemName] = useState("");
  const [errors, setErrors] = useState<PanelError[]>([]);
  const [loading, setLoading] = useState(false);
  const [backendUnavailable, setBackendUnavailable] = useState(false);
  const { confirm, ConfirmDialogHost } = useConfirm();

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return groups;
    return groups.filter((g) => g.groupName.toLowerCase().includes(q));
  }, [groups, search]);

  const loadGroups = async () => {
    const list = await listEnumDictionaryGroups();
    if (list === null) { setBackendUnavailable(true); return; }
    setGroups(list);
    setBackendUnavailable(false);
  };

  const loadDetail = async (groupId: string) => {
    const d = await getEnumDictionaryGroup(groupId);
    setDetail(d);
    if (d) {
      setDraftName(d.groupName);
      setDraftIndexNums(d.itemsIndexNums.join(", "));
    }
  };

  useEffect(() => { loadGroups(); }, []);

  useEffect(() => {
    if (selectedId) loadDetail(selectedId);
    else setDetail(null);
  }, [selectedId]);

  const handleShowAll = async () => {
    setSearch("");
    await loadGroups();
  };

  const handleCreateGroup = async () => {
    await runAdminSubmit({
      confirm,
      confirmKind: "create",
      label: "enum グループ",
      run: async () => {
        setLoading(true);
        setErrors([]);
        try {
          await createEnumDictionaryGroup(newGroupName.trim());
          setModalOpen(false);
          setNewGroupName("");
          await loadGroups();
        } catch (e) {
          setErrors([{ message: String(e) }]);
        } finally {
          setLoading(false);
        }
      },
    });
  };

  const handleSaveGroup = async () => {
    if (!selectedId) return;
    await runAdminSubmit({
      confirm,
      confirmKind: "update",
      label: "enum グループ",
      run: async () => {
        setLoading(true);
        setErrors([]);
        try {
          await updateEnumDictionaryGroup(selectedId, draftName.trim());
          const nums = draftIndexNums.split(",").map((s) => parseInt(s.trim(), 10))
            .filter((n) => !Number.isNaN(n));
          await setEnumDictionaryGroupItems(selectedId, nums);
          await loadGroups();
          await loadDetail(selectedId);
        } catch (e) {
          setErrors([{ message: String(e) }]);
        } finally {
          setLoading(false);
        }
      },
    });
  };

  const handleDeleteGroup = async () => {
    if (!selectedId) return;
    if (!(await confirm("この enum グループを削除します。よろしいですか？", { variant: "danger" }))) {
      return;
    }
    setLoading(true);
    setErrors([]);
    try {
      await deleteEnumDictionaryGroup(selectedId);
      setSelectedId("");
      setDetail(null);
      await loadGroups();
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  const handleAddItem = async () => {
    if (!newItemName.trim()) return;
    await runAdminSubmit({
      confirm,
      confirmKind: "create",
      label: "enum 項目",
      run: async () => {
        setLoading(true);
        try {
          await createEnumDictionaryItem(newItemName.trim());
          setNewItemName("");
          if (selectedId) await loadDetail(selectedId);
        } catch (e) {
          setErrors([{ message: String(e) }]);
        } finally {
          setLoading(false);
        }
      },
    });
  };

  const handleDeleteItem = async (indexNum: number) => {
    if (!(await confirm(`enum 項目 index ${indexNum} を削除します。よろしいですか？`, { variant: "danger" }))) {
      return;
    }
    setLoading(true);
    try {
      await deleteEnumDictionaryItem(indexNum);
      if (selectedId) await loadDetail(selectedId);
    } catch (e) {
      setErrors([{ message: String(e) }]);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="mx-auto max-w-5xl space-y-4 p-4">
      <h1 class="text-lg font-semibold text-gray-900">enum 名簿</h1>
      {backendUnavailable && (
        <p class="text-sm text-amber-700">バックエンドに接続できません。</p>
      )}
      <ValidationErrorPanel errors={errors} />

      <section class="rounded-lg border border-gray-200 bg-white p-4 space-y-3">
        <div class="flex flex-wrap gap-2 items-center">
          <input
            type="search"
            class="input-base flex-1 min-w-[200px]"
            placeholder="グループ名で検索"
            value={search}
            onInput={(e) => setSearch((e.target as HTMLInputElement).value)}
          />
          <button type="button" class="btn-secondary text-sm" onClick={handleShowAll} disabled={loading}>
            全件出力
          </button>
          <button type="button" class="btn-primary text-sm" onClick={() => setModalOpen(true)}>
            新規追加
          </button>
          <span class="text-xs text-gray-500">{filtered.length} 件</span>
        </div>

        <table class="w-full text-xs">
          <thead>
            <tr class="border-b text-left text-gray-500">
              <th class="px-2 py-1">index</th>
              <th class="px-2 py-1">グループ名</th>
              <th class="px-2 py-1">groupId</th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((g) => (
              <tr
                key={g.groupId}
                class={`border-b cursor-pointer hover:bg-gray-50 ${selectedId === g.groupId ? "bg-blue-50" : ""}`}
                onClick={() => setSelectedId(g.groupId)}
              >
                <td class="px-2 py-1 font-mono">{g.indexNum}</td>
                <td class="px-2 py-1">{g.groupName}</td>
                <td class="px-2 py-1 font-mono text-gray-500">{g.groupId}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {selectedId && detail && (
        <section class="rounded-lg border border-blue-200 bg-blue-50 p-4 space-y-3">
          <h2 class="text-sm font-semibold text-blue-900">インライン編集</h2>
          <label class="block text-xs font-medium text-gray-700">グループ名</label>
          <input class="input-base w-full max-w-md" value={draftName}
            onInput={(e) => setDraftName((e.target as HTMLInputElement).value)} />
          <label class="block text-xs font-medium text-gray-700">含める item index（カンマ区切り）</label>
          <input class="input-base w-full max-w-md font-mono" value={draftIndexNums}
            onInput={(e) => setDraftIndexNums((e.target as HTMLInputElement).value)} />
          <ul class="text-xs text-gray-700">
            {detail.items.map((it) => (
              <li key={it.indexNum} class="flex items-center gap-2">
                <span class="font-mono">{it.indexNum}</span> — {it.name}
                <button type="button" class="btn-danger text-xs" disabled={loading}
                  onClick={() => handleDeleteItem(it.indexNum)}>削除</button>
              </li>
            ))}
          </ul>
          <div class="flex flex-wrap gap-2">
            <button type="button" class="btn-primary text-sm" disabled={loading} onClick={handleSaveGroup}>
              保存
            </button>
            <button type="button" class="btn-danger text-sm" disabled={loading} onClick={handleDeleteGroup}>
              グループ削除
            </button>
          </div>
        </section>
      )}

      <section class="rounded-lg border border-gray-200 bg-white p-4 space-y-2">
        <h2 class="text-sm font-semibold text-gray-800">enum 項目の新規追加</h2>
        <div class="flex gap-2">
          <input class="input-base flex-1 max-w-md" placeholder="項目名" value={newItemName}
            onInput={(e) => setNewItemName((e.target as HTMLInputElement).value)} />
          <button type="button" class="btn-secondary text-sm" disabled={loading} onClick={handleAddItem}>
            項目を追加
          </button>
        </div>
      </section>

      <Modal open={modalOpen} title="enum グループを追加" onClose={() => setModalOpen(false)}
        footer={(
          <div class="flex justify-end gap-2">
            <button type="button" class="btn-secondary text-sm" onClick={() => setModalOpen(false)}>キャンセル</button>
            <button type="button" class="btn-primary text-sm" disabled={loading || !newGroupName.trim()}
              onClick={handleCreateGroup}>登録</button>
          </div>
        )}
      >
        <label class="block text-xs font-medium text-gray-700 mb-1">グループ名</label>
        <input class="input-base w-full" value={newGroupName}
          onInput={(e) => setNewGroupName((e.target as HTMLInputElement).value)} />
      </Modal>

      <ConfirmDialogHost />
    </div>
  );
}
