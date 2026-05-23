# existing_pr_update prompt router

## purpose
Follow-up work for an already-open PR.

## trigger_condition
Worktype is `existing_pr_update`.

## required_reads
- .agent/protocols/completion-summary.md

## optional_reads
- relevant protocol(s) for touched surface

## protocol_triggers
- completion-summary protocol

## output_shape
follow-up delta, checks, output sink state

## out_of_scope
- replacing required PR follow-up comment sink with PR body-only edits
