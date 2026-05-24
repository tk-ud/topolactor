import { JSX } from "preact";
import {
  mergeDesignClassName,
  mergeDesignStyle,
  type ComponentDesignParams,
  type ComponentIdentityProps,
} from "./types.ts";

export type TreeNodeData = {
  key: string;
  label: string;
  children?: TreeNodeData[];
  disabled?: boolean;
  data?: unknown;
};

export type TreeNodeProps = {
  node: TreeNodeData;
  expandedKeys: Set<string>;
  selectedKey?: string;
  onExpand: (key: string, expanded: boolean) => void;
  onSelect?: (key: string, node: TreeNodeData) => void;
  depth: number;
};

export function TreeNode({
  node,
  expandedKeys,
  selectedKey,
  onExpand,
  onSelect,
  depth,
}: TreeNodeProps): JSX.Element {
  const hasChildren = Array.isArray(node.children) && node.children.length > 0;
  const isExpanded = expandedKeys.has(node.key);
  const isSelected = selectedKey === node.key;
  return (
    <li style="list-style:none;padding:0;margin:0">
      <div
        style={`display:flex;align-items:center;gap:4px;padding:3px 0 3px ${depth * 16}px;cursor:${node.disabled ? "not-allowed" : "pointer"};background:${isSelected ? "#e8f0fe" : "transparent"};border-radius:4px;opacity:${node.disabled ? 0.5 : 1}`}
        onClick={() => {
          if (node.disabled) return;
          if (hasChildren) onExpand(node.key, !isExpanded);
          onSelect?.(node.key, node);
        }}
      >
        {hasChildren
          ? (
            <span
              style="width:16px;text-align:center;font-size:0.75rem;color:#888;user-select:none"
              aria-hidden="true"
            >
              {isExpanded ? "▼" : "▶"}
            </span>
          )
          : <span style="width:16px" />}
        <span style={`font-size:0.9rem;font-family:monospace;color:${isSelected ? "#0070f3" : "#333"}`}>
          {node.label}
        </span>
      </div>
      {hasChildren && isExpanded && (
        <ul style="margin:0;padding:0">
          {(node.children ?? []).map((child) => (
            <TreeNode
              key={child.key}
              node={child}
              expandedKeys={expandedKeys}
              selectedKey={selectedKey}
              onExpand={onExpand}
              onSelect={onSelect}
              depth={depth + 1}
            />
          ))}
        </ul>
      )}
    </li>
  );
}

export type TreeProps = ComponentIdentityProps & {
  nodes: TreeNodeData[];
  expandedKeys?: Set<string>;
  selectedKey?: string;
  onExpand?: (key: string, expanded: boolean) => void;
  onSelect?: (key: string, node: TreeNodeData) => void;
  className?: string;
  design?: ComponentDesignParams;
};

export function Tree({
  nodes,
  expandedKeys = new Set(),
  selectedKey,
  onExpand,
  onSelect,
  className,
  design,
}: TreeProps): JSX.Element {
  const mergedClassName = mergeDesignClassName(className, design);
  const mergedStyle = mergeDesignStyle("font-family:monospace", design);
  return (
    <ul
      role="tree"
      className={mergedClassName}
      style={`${mergedStyle};margin:0;padding:0`}
    >
      {nodes.map((node) => (
        <TreeNode
          key={node.key}
          node={node}
          expandedKeys={expandedKeys}
          selectedKey={selectedKey}
          onExpand={onExpand ?? (() => {})}
          onSelect={onSelect}
          depth={0}
        />
      ))}
    </ul>
  );
}
