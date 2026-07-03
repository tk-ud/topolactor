#!/usr/bin/env python3
"""check_sql_attention_ssot_yaml.py -- SQL Attention SSOT YAML/roadmap alignment checks.

Python3 stdlib structured-processing counterpart of the Ruby heredoc
previously embedded in check-sql-attention-ssot.sh.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "lib"))
import minimal_yaml as yaml  # noqa: E402

dig = yaml.dig
arr = yaml.arr


def fail(msg):
    sys.stderr.write(f"FAIL: {msg}\n")
    sys.exit(1)


def assert_eq(actual, expected, label):
    if actual != expected:
        fail(f"{label} expected={expected!r} actual={actual!r}")


def assert_includes(items, value, label):
    if not isinstance(items, list) or value not in items:
        fail(f"{label} missing {value!r}")


def main():
    ssot = yaml.load_file("docs/design/sql-attention-logs-ssot.yaml")
    roadmap = yaml.load_file("docs/system-roadmap.yaml")

    assert_eq(ssot.get("id"), "sql_attention_logs_ssot", "ssot.id")
    assert_eq(ssot.get("status"), "structural_ssot", "ssot.status")
    assert_eq(ssot.get("semantic_markdown"), "docs/design/sql-attention-logs-ssot.md", "ssot.semantic_markdown")
    assert_eq(dig(ssot, "ssot_roles", "markdown"), "semantic_formula_and_route_ssot", "ssot_roles.markdown")
    assert_eq(dig(ssot, "ssot_roles", "yaml"), "structural_policy_schema_runtime_contract_ssot", "ssot_roles.yaml")

    assert_eq(dig(ssot, "sql_attention_target", "target_schema"), "hubs", "sql_attention_target.target_schema")
    for v in ["tensor", "attractor", "collapse_point"]:
        assert_includes(dig(ssot, "sql_attention_target", "target_semantics"), v, "sql_attention_target.target_semantics")
    for v in ["topology", "registry"]:
        assert_includes(dig(ssot, "sql_attention_target", "not_target"), v, "sql_attention_target.not_target")
    for k in ["logs_current", "logs_attention", "phase_attention"]:
        if not isinstance(dig(ssot, "layers", k), dict):
            fail(f"layers.{k} missing")
    if not isinstance(dig(ssot, "scheduler_runtime_boundary", "owns"), list):
        fail("scheduler_runtime_boundary.owns missing")
    assert_eq(dig(ssot, "evidence_layer_separation", "invariant"), "no_single_score_collapse", "evidence_layer_separation.invariant")

    fields = dig(ssot, "layers", "logs_attention", "schema_contract", "suggested_fields")
    for f in ["current_id", "statistics_json", "ema_score", "l2_norm", "vector_json", "phase_vector_json",
              "neighbor_score", "evidence_json", "archive_policy"]:
        assert_includes(fields, f, "logs_attention.schema_contract.suggested_fields")

    semantics = dig(ssot, "layers", "logs_attention", "schema_contract", "persistence_semantics")
    for v in [
        "append_only",
        "archive_required",
        "must_reference_logs_current_current_id",
        "statistics_json_and_ema_score_store_stable_confidence_layer",
        "vector_json_stores_convergent_neighbor_hit_vector",
        "phase_vector_json_stores_exploratory_phase_shifted_candidate_vector",
    ]:
        assert_includes(semantics, v, "logs_attention.schema_contract.persistence_semantics")

    phase_invariants = arr(dig(ssot, "layers", "phase_attention", "invariants")) + arr(
        dig(ssot, "phase_attention_function_contract", "invariants")
    )
    for v in [
        "phase_attention_is_post_main_auxiliary_route",
        "phase_vector_is_candidate_evidence_not_adopted_topology_state",
        "no_direct_registry_mutation",
        "no_automatic_migration_execution",
        "no_automatic_column_promotion",
        "phase_movement_is_not_manifest_or_policy_cap_derived",
    ]:
        if v not in phase_invariants:
            fail(f"phase invariants missing {v}")

    pa_quat = dig(ssot, "layers", "phase_attention", "quaternion_semantics")
    if not isinstance(pa_quat, dict):
        fail("layers.phase_attention.quaternion_semantics missing")
    if "x_y_z" in pa_quat:
        fail("phase_attention.quaternion_semantics must not use x_y_z key (canonical is separate x, y, z)")
    if "hubs_hub_relations" not in str(pa_quat.get("x", "")):
        fail("phase_attention.quaternion_semantics.x must reference hubs_hub_relations")
    if "topology_manifest_id" not in str(pa_quat.get("y", "")):
        fail("phase_attention.quaternion_semantics.y must reference topology_manifest_id")
    if "hub_id" not in str(pa_quat.get("z", "")):
        fail("phase_attention.quaternion_semantics.z must reference hub_id")

    pa_calc = dig(ssot, "phase_attention_function_contract", "calculation")
    if not isinstance(pa_calc, dict):
        fail("phase_attention_function_contract.calculation missing")
    if "x_y_z" in pa_calc:
        fail("phase_attention_function_contract.calculation must not use x_y_z key")
    if "hubs_hub_relations" not in str(pa_calc.get("x", "")):
        fail("phase_attention_function_contract.calculation.x must reference hubs_hub_relations")
    if "topology_manifest_id" not in str(pa_calc.get("y", "")):
        fail("phase_attention_function_contract.calculation.y must reference topology_manifest_id")
    if "hub_id" not in str(pa_calc.get("z", "")):
        fail("phase_attention_function_contract.calculation.z must reference hub_id")

    detailed = dig(roadmap, "system_roadmap_ssot", "related_surfaces", "detailed_design_ssot")
    for v in ["docs/design/sql-attention-logs-ssot.md", "docs/design/sql-attention-logs-ssot.yaml"]:
        assert_includes(detailed, v, "roadmap.related_surfaces.detailed_design_ssot")

    milestones = dig(roadmap, "system_roadmap_ssot", "milestones")
    if not (isinstance(milestones, dict) and "M7_sql_attention_observation_runtime" in milestones):
        fail("milestones.M7_sql_attention_observation_runtime missing")

    registry = dig(roadmap, "system_roadmap_ssot", "implementation_registry")
    for k in [
        "backend.sql_attention_logs_current",
        "backend.sql_attention_hub_current",
        "backend.sql_attention_scheduler_exploration",
        "backend.sql_attention_logs_attention_persistence",
        "backend.sql_attention_phase_vector",
        "backend.sql_attention_topology_projection",
    ]:
        if not (isinstance(registry, dict) and k in registry):
            fail(f"implementation_registry missing {k}")



if __name__ == "__main__":
    main()
    print("PASS check-sql-attention-ssot-yaml assertions=2")
