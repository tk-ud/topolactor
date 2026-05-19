# Runtime Boundary Failure Matrix

This protocol is condition-triggered. It is not an always-on read.

## Trigger scope

Run this matrix verification when changes include:

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

Any matrix item marked failure, unverified, or unjustified out-of-scope is blocking under Recursive Verification Gate.


For boundary-expanding changes, include End-to-End Boundary Identity verification across:

- DB identity
- backend contract/event identity
- API request/response payload identity
- Repository mutation identity
- Frontend projection identity
- UI action identity
