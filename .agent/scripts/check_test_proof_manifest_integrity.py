#!/usr/bin/env python3
"""check_test_proof_manifest_integrity.py -- audits the system-wide test proof manifest SSOT.

Python3 stdlib structured-processing counterpart of the Ruby heredoc
previously embedded in check-test-proof-manifest-integrity.sh.
"""
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "lib"))
import minimal_yaml as yaml  # noqa: E402

dig = yaml.dig
arr = yaml.arr


def main():
    manifest_path = "docs/design/test-proof-manifest-ssot.yaml"
    reverse_lookup_path = ".agent/docs/test-bundles.yaml"
    ssot_map_path = ".agent/docs/ssot-map.yaml"

    manifest = yaml.load_file(manifest_path)
    reverse_lookup = yaml.load_file(reverse_lookup_path)
    ssot_map = yaml.load_file(ssot_map_path)
    failures = []

    proofs = manifest.get("chronological_scope_index")
    if proofs is None:
        failures.append("missing chronological_scope_index")
        proofs = []
    required = dig(manifest, "proof_model", "required_fields") or []
    proof_types = dig(manifest, "proof_model", "proof_types") or []
    ids = {}
    orders = []

    def workflow_file_for(workflow_job):
        return str(workflow_job).split("::", 1)[0]

    for index, proof in enumerate(proofs):
        label = proof.get("proof_id") or f"index {index}"
        for field in required:
            if field not in proof:
                failures.append(f"{label}: missing required field {field}")

        pid = proof.get("proof_id")
        if pid:
            if pid in ids:
                failures.append(f"duplicate proof_id {pid}")
            ids[pid] = proof

        order = proof.get("proof_order")
        if not isinstance(order, int) or isinstance(order, bool):
            failures.append(f"{label}: proof_order must be integer")
        else:
            orders.append((order, label))

        if proof.get("proof_type") not in proof_types:
            failures.append(f"{label}: invalid proof_type {proof.get('proof_type')!r}")

        for field in ["ssot_refs", "implementation_files", "test_files", "runner_surfaces", "evidence_inputs",
                      "workflow_jobs", "depends_on", "unblocks", "proves", "does_not_prove"]:
            if not isinstance(proof.get(field), list):
                failures.append(f"{label}: {field} must be a list")

        changed = dig(proof, "required_when", "changed_files")
        if not (isinstance(changed, list) and len(changed) > 0):
            failures.append(f"{label}: required_when.changed_files must be a non-empty list")

        for path in arr(proof.get("implementation_files")):
            if not os.path.exists(path):
                failures.append(f"{label}: implementation_files path does not exist: {path}")

        for path in arr(proof.get("runner_surfaces")):
            if not os.path.exists(path):
                failures.append(f"{label}: runner_surfaces path does not exist: {path}")

        for path in arr(proof.get("evidence_inputs")):
            if not os.path.exists(path):
                failures.append(f"{label}: evidence_inputs path does not exist: {path}")

        for workflow_job in arr(proof.get("workflow_jobs")):
            workflow_file = workflow_file_for(workflow_job)
            if not os.path.isfile(workflow_file):
                failures.append(f"{label}: workflow_jobs workflow file does not exist: {workflow_job}")

        for path in arr(proof.get("test_files")):
            if path.startswith("docs/design/") or path.startswith("db/") or path.startswith(".agent/tests/") or path.startswith(".github/"):
                failures.append(f"{label}: test_files contains non-test/evidence/runner path {path}")
            if not ("/tests/" in path or path.endswith(".test.ts") or path.endswith(".csproj")):
                failures.append(f"{label}: test_files must contain executable test files/projects only: {path}")
            if not os.path.exists(path):
                failures.append(f"{label}: test_files path does not exist: {path}")

        for path in arr(proof.get("ssot_refs")):
            if not os.path.isfile(path):
                failures.append(f"{label}: ssot_refs path does not exist; move to missing_ssot_blocking: {path}")

        for path in arr(proof.get("missing_ssot_blocking")):
            if os.path.isfile(path):
                failures.append(f"{label}: missing_ssot_blocking path exists and should be moved to ssot_refs: {path}")

        if proof.get("proof_type") == "known_gap":
            if str(proof.get("known_gap_reason") or "").strip() == "":
                failures.append(f"{label}: known_gap must have known_gap_reason")
            if len(arr(proof.get("proves"))) != 0:
                failures.append(f"{label}: known_gap must not claim proves entries")
        else:
            if len(arr(proof.get("proves"))) == 0:
                failures.append(f"{label}: implemented/design proof must have at least one proves entry")
            if "missing_ssot_blocking" in proof:
                failures.append(f"{label}: non-gap proof must not carry missing_ssot_blocking")

    for (prev_order, prev_id), (order, oid) in zip(orders, orders[1:]):
        if not order > prev_order:
            failures.append(f"proof_order must be strictly increasing: {prev_id}({prev_order}) before {oid}({order})")

    for proof in proofs:
        label = proof.get("proof_id")
        for dep in arr(proof.get("depends_on")):
            if dep not in ids:
                failures.append(f"{label}: depends_on unknown proof_id {dep}")
            dep_proof = ids.get(dep)
            if dep_proof is not None:
                dep_order = dep_proof.get("proof_order") or 0
                own_order = proof.get("proof_order") or 0
                if dep_order >= own_order:
                    failures.append(f"{label}: depends_on {dep} must have lower proof_order")
                if proof.get("proof_type") != "known_gap" and dep_proof.get("proof_type") == "known_gap":
                    allowed = proof.get("depends_on_known_gap_allowed") is True and str(
                        proof.get("depends_on_known_gap_reason") or ""
                    ).strip() != ""
                    if not allowed:
                        failures.append(
                            f"{label}: non-gap proof depends_on known_gap {dep}; remove dependency or set "
                            "depends_on_known_gap_allowed with reason"
                        )
        for target in arr(proof.get("unblocks")):
            if target == "frontend-admin-projection-expression-e2e-completion":
                continue
            if target not in ids:
                failures.append(f"{label}: unblocks unknown proof_id {target}")

    for index, bundle in enumerate(arr(reverse_lookup.get("reverse_lookup"))):
        label = bundle.get("bundle_id") or f"reverse_lookup index {index}"
        for proof_ref in arr(bundle.get("proof_refs")):
            if proof_ref not in ids:
                failures.append(f"{reverse_lookup_path}: {label}: proof_refs unknown proof_id {proof_ref}")

    ssot_map_entries = arr(ssot_map.get("mapping"))
    test_proof_entries = [e for e in ssot_map_entries if manifest_path in arr(e.get("required_docs"))]
    if not test_proof_entries:
        failures.append(f"{ssot_map_path}: {manifest_path} must be discoverable via required_docs")
    else:
        required_surface_terms = ["test-proof-manifest-ci-gate", "proof graph", "manifest integrity", "CI proof audit surface"]
        surface_terms = []
        for e in test_proof_entries:
            surface_terms.extend(arr(e.get("change_surfaces")))
        for term in required_surface_terms:
            if term not in surface_terms:
                failures.append(f"{ssot_map_path}: missing discovery change surface {term}")

    if not failures:
        print(
            f"OK  [test-proof-manifest] {len(proofs)} proof edges and "
            f"{len(arr(reverse_lookup.get('reverse_lookup')))} reverse lookup bundles passed integrity checks"
        )
        return 0
    for f in failures:
        sys.stderr.write(f"FAIL [test-proof-manifest] {f}\n")
    return 1


if __name__ == "__main__":
    sys.exit(main())
