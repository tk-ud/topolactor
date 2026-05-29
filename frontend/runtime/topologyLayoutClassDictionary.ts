// Generated-from-contract artifact source: docs/design/topology-layout-class-ssot.yaml
// Projection artifact only. Not a secondary SSOT.

export type TopologyLayoutClassDictionaryEntry = {
  classKey: string;
  className: string;
  category: string;
  projectionScope: string[];
  semanticRole: string;
  allowedFor: string[];
};

export const TOPOLOGY_LAYOUT_CLASS_DICTIONARY: TopologyLayoutClassDictionaryEntry[] = [
  {
    classKey: "layout.root.grid",
    className: "topolactor-topology-layout-root-grid",
    category: "layout",
    projectionScope: ["topology_layout"],
    semanticRole: "root_grid_container",
    allowedFor: ["layout_root"],
  },
  {
    classKey: "layout.section.stack",
    className: "topolactor-topology-layout-section-stack",
    category: "layout",
    projectionScope: ["topology_layout"],
    semanticRole: "stacked_section",
    allowedFor: ["layout_section"],
  },
  {
    classKey: "layout.row.flex",
    className: "topolactor-topology-layout-row-flex",
    category: "layout",
    projectionScope: ["topology_layout"],
    semanticRole: "row_flex_container",
    allowedFor: ["layout_row"],
  },
  {
    classKey: "layout.card.surface",
    className: "topolactor-topology-layout-card-surface",
    category: "surface",
    projectionScope: ["topology_component_wrapper"],
    semanticRole: "projected_card_surface",
    allowedFor: ["component_wrapper"],
  },
  {
    classKey: "layout.node.boundary",
    className: "topolactor-topology-layout-node-boundary",
    category: "boundary",
    projectionScope: ["topology_component_wrapper"],
    semanticRole: "projected_node_boundary",
    allowedFor: ["component_wrapper"],
  },
  {
    classKey: "layout.state.selected",
    className: "topolactor-topology-layout-state-selected",
    category: "state",
    projectionScope: ["topology_layout_preview"],
    semanticRole: "selected_projection_node",
    allowedFor: ["preview_state"],
  },
  {
    classKey: "layout.state.blocked",
    className: "topolactor-topology-layout-state-blocked",
    category: "state",
    projectionScope: ["topology_layout_preview"],
    semanticRole: "blocked_projection_node",
    allowedFor: ["preview_state"],
  },
];
