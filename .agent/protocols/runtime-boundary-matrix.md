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

If any item is out of scope, state reason in completion report or PR summary.
