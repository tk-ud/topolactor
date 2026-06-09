import type { AggregationBlock, AggregationMeasure } from "./aggregationMeasures.ts";
import type {
  HavingCondition,
  SearchCondition,
  SearchOperator,
} from "./manifestScreenDesign.ts";

function parseSearchTargets(raw: string): string[] {
  return raw
    .split(/[,;\n]/)
    .map((s) => s.trim())
    .filter(Boolean);
}

const BLOCK_SEPARATOR = "\n---\n";
const MEASURE_RE = /^(\w+)\(([^)]+)\)$/i;
const HAVING_RE = /^(\w+)\(([^)]+)\)\s*(=|!=|<>|>=|<=|>|<)\s*(.+)$/i;

const SEARCH_OPS: SearchOperator[] = [
  "=", "!=", "<>", "like", "ilike", "not like",
  ">", ">=", "<", "<=", "between", "in", "not in",
  "is null", "is not null",
];

export function formatSearchTargetsRaw(columns: string[]): string {
  return columns.map((c) => c.trim()).filter(Boolean).join(", ");
}

export function parseSearchTargetsToColumns(raw: string): string[] {
  return parseSearchTargets(raw);
}

function quoteSqlScalar(value: string): string {
  const v = value.trim();
  if (/^-?\d+(\.\d+)?$/.test(v)) return v;
  return `'${v.replace(/'/g, "''")}'`;
}

function unquoteSqlScalar(raw: string): string {
  const v = raw.trim();
  if (
    (v.startsWith("'") && v.endsWith("'")) ||
    (v.startsWith('"') && v.endsWith('"'))
  ) {
    return v.slice(1, -1).replace(/''/g, "'");
  }
  return v;
}

function formatWhereClause(conditions: SearchCondition[]): string {
  if (conditions.length === 0) return "";
  const parts: string[] = [];
  for (let i = 0; i < conditions.length; i++) {
    const c = conditions[i];
    if (i > 0) {
      parts.push((conditions[i - 1].logicalConnector ?? "and").toUpperCase());
    }
    const col = c.column.trim();
    switch (c.operator) {
      case "is null":
        parts.push(`${col} IS NULL`);
        break;
      case "is not null":
        parts.push(`${col} IS NOT NULL`);
        break;
      case "between":
        parts.push(
          `${col} BETWEEN ${quoteSqlScalar(c.value ?? "")} AND ${quoteSqlScalar(c.valueTo ?? "")}`,
        );
        break;
      case "in":
      case "not in": {
        const vals = (c.values ?? []).map(quoteSqlScalar).join(", ");
        parts.push(`${col} ${c.operator.toUpperCase()} (${vals})`);
        break;
      }
      default:
        parts.push(`${col} ${c.operator.toUpperCase()} ${quoteSqlScalar(c.value ?? "")}`);
    }
  }
  return parts.join(" ");
}

function formatHavingClause(conditions: HavingCondition[]): string {
  if (conditions.length === 0) return "";
  return conditions.map((h) =>
    `${h.function}(${h.column}) ${h.operator.toUpperCase()} ${quoteSqlScalar(h.value)}`
  ).join(" AND ");
}

function formatAggregationBlock(block: AggregationBlock): string {
  const lines: string[] = [];
  const sourceRef = block.sourceRef.trim();
  const aggregationKey = block.aggregationKey.trim();
  const measures = block.measures.filter((m) => m.column.trim() && m.function.trim());

  if (sourceRef) lines.push(`FROM ${sourceRef}`);

  const selectParts: string[] = [];
  if (aggregationKey) selectParts.push(aggregationKey);
  for (const m of measures) {
    selectParts.push(`${m.function}(${m.column})`);
  }
  if (selectParts.length > 0) lines.push(`SELECT ${selectParts.join(", ")}`);

  const where = formatWhereClause(block.searchConditions ?? []);
  if (where) lines.push(`WHERE ${where}`);

  if (aggregationKey) lines.push(`GROUP BY ${aggregationKey}`);

  const having = formatHavingClause(block.havingConditions ?? []);
  if (having) lines.push(`HAVING ${having}`);

  return lines.join("\n");
}

/** Serialize aggregation blocks to SQL-shaped text (bidirectional with structured UI). */
export function formatAggregationSpecFromBlocks(blocks: AggregationBlock[]): string {
  const active = blocks.filter((b) =>
    b.sourceRef.trim() || b.aggregationKey.trim()
    || b.measures.some((m) => m.column.trim() && m.function.trim())
    || (b.searchConditions ?? []).length > 0
    || (b.havingConditions ?? []).length > 0
  );
  if (active.length === 0) return "";
  return active.map(formatAggregationBlock).join(BLOCK_SEPARATOR);
}

export type ParseAggregationSpecResult =
  | { ok: true; blocks: AggregationBlock[] }
  | { ok: false; error: string };

function parseMeasureToken(token: string): AggregationMeasure | null {
  const m = MEASURE_RE.exec(token.trim());
  if (!m) return null;
  return { function: m[1].toLowerCase(), column: m[2].trim() };
}

function parseLegacyAggregationFragment(
  raw: string,
  defaultSourceRef: string,
): ParseAggregationSpecResult {
  const groupByMatch = /\bGROUP\s+BY\s+(.+)$/i.exec(raw);
  if (!groupByMatch) {
    return { ok: false, error: "GROUP BY が見つかりません" };
  }
  const aggregationKey = groupByMatch[1].trim();
  const beforeGroup = raw.slice(0, groupByMatch.index).trim();
  const measures = beforeGroup
    .split(",")
    .map((t) => parseMeasureToken(t))
    .filter((m): m is AggregationMeasure => m !== null);
  const sourceRef = measures[0]
    ? measures[0].column.split(".")[0] ?? defaultSourceRef
    : aggregationKey.split(".")[0] ?? defaultSourceRef;
  return {
    ok: true,
    blocks: [{
      sourceRef: sourceRef.trim(),
      aggregationKey,
      measures,
      searchConditions: [],
      havingConditions: [],
    }],
  };
}

function splitSelectList(selectBody: string): string[] {
  const parts: string[] = [];
  let depth = 0;
  let current = "";
  for (const ch of selectBody) {
    if (ch === "(") depth++;
    if (ch === ")") depth = Math.max(0, depth - 1);
    if (ch === "," && depth === 0) {
      if (current.trim()) parts.push(current.trim());
      current = "";
      continue;
    }
    current += ch;
  }
  if (current.trim()) parts.push(current.trim());
  return parts;
}

function parseSingleSearchCondition(fragment: string): SearchCondition | null {
  const text = fragment.trim();
  if (!text) return null;

  const isNull = /^(.+?)\s+IS\s+NULL$/i.exec(text);
  if (isNull) {
    return { column: isNull[1].trim(), operator: "is null" };
  }
  const isNotNull = /^(.+?)\s+IS\s+NOT\s+NULL$/i.exec(text);
  if (isNotNull) {
    return { column: isNotNull[1].trim(), operator: "is not null" };
  }
  const between = /^(.+?)\s+BETWEEN\s+(.+?)\s+AND\s+(.+)$/i.exec(text);
  if (between) {
    return {
      column: between[1].trim(),
      operator: "between",
      value: unquoteSqlScalar(between[2]),
      valueTo: unquoteSqlScalar(between[3]),
    };
  }
  const inList = /^(.+?)\s+(NOT\s+IN|IN)\s*\((.+)\)$/i.exec(text);
  if (inList) {
    const operator = inList[2].toLowerCase().replace(/\s+/g, " ") as "in" | "not in";
    const values = splitSelectList(inList[3]).map(unquoteSqlScalar);
    return { column: inList[1].trim(), operator, values };
  }

  for (const op of SEARCH_OPS) {
    if (op === "is null" || op === "is not null" || op === "between" || op === "in" || op === "not in") {
      continue;
    }
    const opPattern = op === "<>" ? "<>" : op.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    const re = new RegExp(`^(.+?)\\s+${opPattern}\\s+(.+)$`, "i");
    const m = re.exec(text);
    if (m) {
      return {
        column: m[1].trim(),
        operator: op,
        value: unquoteSqlScalar(m[2]),
      };
    }
  }
  return null;
}

function parseWhereClause(whereBody: string): SearchCondition[] {
  const tokens = splitWhereOnConnectors(whereBody.trim());
  const conditions: SearchCondition[] = [];
  for (let i = 0; i < tokens.length; i++) {
    const cond = parseSingleSearchCondition(tokens[i].text);
    if (!cond) continue;
    if (i > 0 && tokens[i].connector) {
      const prev = conditions[conditions.length - 1];
      conditions[conditions.length - 1] = {
        ...prev,
        logicalConnector: tokens[i].connector,
      };
    }
    conditions.push(cond);
  }
  return conditions;
}

function splitWhereOnConnectors(
  text: string,
): { connector?: "and" | "or" | "not"; text: string }[] {
  const parts: { connector?: "and" | "or" | "not"; text: string }[] = [];
  const re = /\s+(AND|OR|NOT)\s+/gi;
  let lastIndex = 0;
  let pendingConnector: "and" | "or" | "not" | undefined;
  let match: RegExpExecArray | null;
  while ((match = re.exec(text)) !== null) {
    const fragment = text.slice(lastIndex, match.index).trim();
    if (fragment) {
      parts.push({ connector: pendingConnector, text: fragment });
      pendingConnector = match[1].toLowerCase() as "and" | "or" | "not";
    }
    lastIndex = match.index + match[0].length;
  }
  const tail = text.slice(lastIndex).trim();
  if (tail) parts.push({ connector: pendingConnector, text: tail });
  return parts.length > 0 ? parts : [{ text }];
}

function parseHavingClause(havingBody: string): HavingCondition[] {
  return splitWhereOnConnectors(havingBody.trim())
    .map((t) => {
      const m = HAVING_RE.exec(t.text.trim());
      if (!m) return null;
      return {
        column: m[2].trim(),
        function: m[1].toLowerCase(),
        operator: m[3] as HavingCondition["operator"],
        value: unquoteSqlScalar(m[4]),
      };
    })
    .filter((h): h is HavingCondition => h !== null);
}

function parseAggregationBlockChunk(
  chunk: string,
  defaultSourceRef: string,
): AggregationBlock | null {
  const lines = chunk.split("\n").map((l) => l.trim()).filter(Boolean);
  if (lines.length === 0) return null;

  let sourceRef = defaultSourceRef.trim();
  let aggregationKey = "";
  const measures: AggregationMeasure[] = [];
  let searchConditions: SearchCondition[] = [];
  let havingConditions: HavingCondition[] = [];

  for (const line of lines) {
    const fromMatch = /^FROM\s+(.+)$/i.exec(line);
    if (fromMatch) {
      sourceRef = fromMatch[1].trim();
      continue;
    }
    const selectMatch = /^SELECT\s+(.+)$/i.exec(line);
    if (selectMatch) {
      for (const token of splitSelectList(selectMatch[1])) {
        const measure = parseMeasureToken(token);
        if (measure) {
          measures.push(measure);
          continue;
        }
        if (!aggregationKey) aggregationKey = token.trim();
      }
      continue;
    }
    const whereMatch = /^WHERE\s+(.+)$/i.exec(line);
    if (whereMatch) {
      searchConditions = parseWhereClause(whereMatch[1]);
      continue;
    }
    const groupMatch = /^GROUP\s+BY\s+(.+)$/i.exec(line);
    if (groupMatch) {
      aggregationKey = groupMatch[1].trim();
      continue;
    }
    const havingMatch = /^HAVING\s+(.+)$/i.exec(line);
    if (havingMatch) {
      havingConditions = parseHavingClause(havingMatch[1]);
    }
  }

  if (!sourceRef && measures[0]) {
    sourceRef = measures[0].column.split(".")[0] ?? defaultSourceRef;
  }
  if (!sourceRef && aggregationKey) {
    sourceRef = aggregationKey.split(".")[0] ?? defaultSourceRef;
  }

  if (
    !sourceRef && !aggregationKey && measures.length === 0
    && searchConditions.length === 0 && havingConditions.length === 0
  ) {
    return null;
  }

  return {
    sourceRef,
    aggregationKey,
    measures,
    searchConditions,
    havingConditions,
  };
}

/** Parse SQL-shaped aggregation text back into structured blocks. */
export function parseAggregationSpecToBlocks(
  raw: string,
  defaultSourceRef = "",
): ParseAggregationSpecResult {
  const trimmed = raw.trim();
  if (!trimmed) return { ok: true, blocks: [] };

  if (!trimmed.includes("\n") && /\bGROUP\s+BY\b/i.test(trimmed)) {
    return parseLegacyAggregationFragment(trimmed, defaultSourceRef);
  }

  const chunks = trimmed.split(BLOCK_SEPARATOR).map((c) => c.trim()).filter(Boolean);
  const blocks: AggregationBlock[] = [];
  for (const chunk of chunks) {
    const block = parseAggregationBlockChunk(chunk, defaultSourceRef);
    if (block) blocks.push(block);
  }
  if (blocks.length === 0) {
    return { ok: false, error: "集計ブロックを解釈できませんでした" };
  }
  return { ok: true, blocks };
}

type RawMirrorFields = {
  searchKeyColumns: string[];
  aggregationBlocks: AggregationBlock[];
  searchTargets: string;
  aggregationSpec: string;
};

/** Keep deprecated raw mirror fields aligned with structured authoring fields. */
export function syncRawFieldsFromStructured<T extends RawMirrorFields>(
  design: T,
  patch: Partial<T>,
): T {
  let next = design;
  if ("searchKeyColumns" in patch && !("searchTargets" in patch)) {
    next = {
      ...next,
      searchTargets: formatSearchTargetsRaw(next.searchKeyColumns),
    };
  }
  const blocksTouched = "aggregationBlocks" in patch
    || "aggregationKey" in patch
    || "aggregationMeasures" in patch
    || "searchConditions" in patch
    || "havingConditions" in patch;
  if (blocksTouched && !("aggregationSpec" in patch)) {
    next = {
      ...next,
      aggregationSpec: formatAggregationSpecFromBlocks(next.aggregationBlocks ?? []),
    };
  }
  return next;
}
