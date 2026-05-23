# Core Rule: Canonical Runtime Route

Read this rule when work touches runtime route, dispatch, persistence, projection, emission, resolver order, or fallback behavior.

## Canonical Runtime Route

```text
stored_topology_data
→ user_operation
→ operation_vector
→ attractor_resolve
→ structure_map_resolve
→ package_resolve
→ schema_resolve
→ component_expand
→ emission_or_projection
```

Do not bypass any step. Do not add silent fallbacks anywhere in this route.

## Explicit Failure

Silent fallback is prohibited.
Infrastructure / manifest / dispatcher / runtime destination / mapping / package / schema / storage unresolved references remain explicit validation errors.
When docs/SSOT defines a runtime fallback event (for example route_missing jump), it must be emitted as an explicit observable emission/jump event, never as a silent default path.

If policy storage, route mapping, manifest mapping, registry mapping, projection mapping, or parameter storage is not implemented yet, return an explicit missing-policy / missing-parameter / missing-mapping status rather than inventing a production fallback.
