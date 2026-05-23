# existing_pr_update prompt router

## purpose
Follow-up work for an already-open PR.

## trigger_condition
Worktype is `existing_pr_update`.

## required_reads
- .agent/protocols/completion-summary.md

## optional_reads
- .agent/docs/ssot-map.yaml when touched surface exists and relevant SSOT/protocol selection is needed
- .agent/docs/required-paths.yaml only when updating `.agent` structure or check expectation vocabulary
- relevant protocol(s) for touched surface

## protocol_triggers
- completion-summary protocol

## output_shape
follow-up delta, checks, output sink state

## out_of_scope
- replacing required PR follow-up comment sink with PR body-only edits
- breaking manual-paste-unit expectations for Completion Summary
