# Instance Port Substrate Implementation TODO

Status: not_started
Roadmap bundle: `product.instance_port_substrate`
Primary SSOT: `docs/design/instance-port-substrate-ssot.yaml`

This file preserves future implementation work. It is not evidence of implemented runtime behavior.

## Bundle-level future work

- [ ] Add DB schema / seed surfaces for `db_instance_port`, `runtime_instance_port`, `instance_connection_policy`, and `instance_function_authority_binding` without plaintext connection strings or endpoint real values.
- [ ] Add `instance_port_runtime` as a sibling runtime lane to `external_port_runtime`.
- [ ] Add abstract function primitive support for `call_instance_postgres_function` and `call_bound_instance_function` with manifest-authorized function/schema/instance/output bindings.
- [ ] Keep existing `call_postgres_function` limited to Topolactor DB `topology.*` functions and fixed Topolactor connectionString.
- [ ] Reuse the DB guarded vault / runtime secret reference model by `reference_key`; do not create a standalone credential runtime or admin UI.
- [ ] Add fail-close tests for missing credential, missing instance policy, missing function/schema binding, timeout, secret projection denial, provider selector attempts, and unauthorized function names.
- [ ] Add guards proving `provider_kind`, `required_by_bundle`, and Wave labels are data only and do not select C# handlers.
- [ ] Treat Wave Main DB / Mirror DB as example consumer instance_authority_key rows only; do not add `WaveRuntimeHandler`, `wave_*` schemas, or Wave semantic authority to the Topolactor DB.

## Explicitly out of scope for the SSOT wiring PR

- `AbstractInstanceRuntimeHandler`
- `InstancePortDispatchRuntime`
- `CallInstancePostgresFunctionPrimitiveAdapter`
- instance port DDL / seed rows
- Wave SQL function implementation
- Wave integration runtime
