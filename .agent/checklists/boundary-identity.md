# Boundary Identity Checklist

Purpose: fail-fast gate for End-to-End Boundary Identity and Multi-instance leakage checks during boundary extension work.

Answer rules:
- Use only: `yes` / `no` / `n/a`
- Fill answers in a temporary PR-specific checklist file, not this template.
- Keep this template answer slots blank.

## Questions

Q1. Does this change extend an existing boundary?
Answer:

Q2. If Q1=yes, did you explicitly identify the existing boundary being extended?
Answer:

Q3. If Q1=yes, did you document DB primary / unique / FK / CHECK identity?
Answer:

Q4. If Q1=yes, did you document contract / event / DTO identity?
Answer:

Q5. If Q1=yes, did you document API request / response identity?
Answer:

Q6. If Q1=yes, did you document repository INSERT / UPSERT conflict identity?
Answer:

Q7. If Q1=yes, did you document repository UPDATE / DELETE WHERE identity?
Answer:

Q8. If Q1=yes, did you document Frontend projection identity?
Answer:

Q9. If Q1=yes, did you document UI action identity?
Answer:

Q10. If Q1=yes, does DB identity match repository mutation identity?
Answer:

Q11. If Q1=yes, did you document a Multi-instance leakage scenario?
Answer:

Q12. If Q1=yes, did you add a minimum leakage detection test?
Answer:

Q13. If Q12=no, did you document an explicit omission reason?
Answer:

Q14. If Q1=yes, did you update remaining TODO surface?
Answer:

Q15. If Q1=yes, did you verify full branch diff against Boundary Extension Scenario?
Answer:
