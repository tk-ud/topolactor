# Runtime Boundary Failure Matrix

Trigger scope:

- endpoint wiring
- frontend API proxy wiring
- repository write
- admin operation
- persistence mutation
- DB-backed registry operation

Verify at least:

1. success path
2. authentication / authorization failure
3. request validation failure
4. malformed id / malformed payload
5. not found
6. persistence constraint failure
7. repository / backend unavailable
8. frontend proxy status propagation
9. UI-visible error state
10. post-write read consistency

Recursive Verification Gate requirements:

- Any matrix item marked failure, unverified, or unjustified out-of-scope is a blocking failure; completion is not allowed.
- If fixable in the same scope, return to fix phase and re-verify matrix coverage.
- Out-of-scope is allowed only with explicit reason in completion report or PR summary.
- For write-path additions, persistence constraint failure and post-write read consistency are mandatory checks, not optional coverage.


For boundary-expanding changes, matrix verification must also include End-to-End Boundary Identity propagation across:

- DB identity
- Backend contract/event identity
- API payload identity
- Frontend projection identity
- UI action identity

