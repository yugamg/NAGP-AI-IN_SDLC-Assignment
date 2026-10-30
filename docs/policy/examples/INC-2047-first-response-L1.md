# INC-2047 first response: L1 draft

> **AI draft, level 1.** Drafted from the exported pack in
> `ops/incidents/INC-2047` only. No live system was queried.
> **Reviewed by:** *(on-call engineer's name; not yet reviewed)*.
> Check before posting:
> `python3 -I docs/architecture/check_citations.py docs/policy/examples/INC-2047-first-response-L1.md`

This is a worked example of the [autonomy policy](../autonomy-incident-first-response.md)
at its day-one level. It is a draft for a human, not a post.

## What is affected

- **No supplier approval notification has been sent since 2026-01-27 16:55Z**
  [E1]. Finance confirms the last one they know of arrived on 27 January
  [E2].
- **Approvals themselves work.** Requests return 200 and the audit trail is
  complete [E3].
- **The scale is unclear: three sources give three different numbers.**
  Treat all of them as unconfirmed.

  | Source | Count |
  | --- | --- |
  | Finance, this morning alone [E4] | "about forty" invoices |
  | Publisher export since the release (`grep -c` over the file) | 15 events |
  | `audit-archive` subscription [E6] | 54 messages held |

  The publisher export says it is filtered [E5], so it may be incomplete.
- **Personal data in the evidence:** worker logs contain supplier e-mail
  addresses. They are redacted from this draft.

## Timeline (UTC)

| Time | Event |
| --- | --- |
| 01-27 16:55 | Last event published as `eventType=InvoiceApproved` [E7]; last notification sent [E1] |
| 01-28 08:42 | Release 2.15.1 swap completed on 3 instances [E8] |
| 01-28 09:03 | First event published as `eventType=invoice.approved` [E9] |
| 01-28 08:00 onwards | Worker health check logs "ok" with **0 messages processed** [E10] |
| 01-29 13:48 | Finance reports missing confirmations [E4] |
| 01-29 14:06 | Alert fires [E11] |

## What changed recently

- 2.15.1 moved event type names to dotted lowercase [E12]. Its release notes
  say consumers filtering on the old names "must update their
  subscriptions" [E13].
- No subscription or subscription rule was changed in the window [E14].

## Hypotheses, ranked (not conclusions)

1. **The `notification-worker` filter still expects the old name.** Its rule
   matches `eventType = InvoiceApproved` [E15], while events have been
   published as `invoice.approved` since the release [E9]. A message that
   matches no rule is simply not delivered to that subscription *(general
   knowledge, verify)*, which fits 0 active and 0 dead-lettered [E16].
   - **Would disprove it:** any `invoice.approved` message having reached
     `notification-worker`. Or a rule list that differs from the one
     exported at 14:28Z.
2. **The worker is stuck, not starved.**
   - **Against:** it has not restarted, it has logged no errors [E17], and
     it processed normally before the release [E1].
   - **Would disprove it:** a test message with the old name being processed
     now.

**Do not trust the health check.** It reports "ok" while processing nothing
[E10]. It measures that the process is alive, not that work is flowing.

## Options for the incident commander (the IC decides)

Constraints for every option:
- each needs a named approver, and out of hours that is the IC [E18];
- a rollback affects the whole release [E19];
- recovering missed notifications is a separate decision from restoring the
  flow [E20].

| Option | What it changes | How to undo it | What else it affects |
| --- | --- | --- | --- |
| A. Add a subscription rule matching `invoice.approved` | Future notifications flow again | Delete the rule | Only `notification-worker` |
| B. Roll back to 2.15.0 | Old event names return | Redeploy 2.15.1 | Also removes the audit export fields and dependency bumps [E12]. Undoes the platform naming alignment |
| C. Replay missed approvals | Sends the notifications that were missed | Not reversible: e-mails cannot be unsent | Suppliers get late confirmations. Needs finance to decide whether they want that. Messages expire 14 days after enqueue [E21] |

The agent recommends none of these. Choosing is the IC's call.

## Citations

| ID | Element | File | Lines | Must contain |
| --- | --- | --- | --- | --- |
| E1 | Last notification sent | `ops/incidents/INC-2047/worker-logs.txt` | 16 | `Notification sent invoiceId=INV-44248` |
| E2 | Last confirmed arrival 27 January | `ops/incidents/INC-2047/alert.md` | 26 | `27 January` |
| E3 | Approvals return 200, audit complete | `ops/incidents/INC-2047/alert.md` | 22-24 | `The approval request returns 200` |
| E4 | Finance: about forty this morning | `ops/incidents/INC-2047/alert.md` | 16 | `about forty invoices` |
| E5 | Publisher export is filtered | `ops/incidents/INC-2047/publisher-logs.txt` | 3 | `filtered to publisher events` |
| E6 | audit-archive holds 54 | `ops/incidents/INC-2047/servicebus-status.txt` | 17 | `audit-archive          54` |
| E7 | Last old-name event | `ops/incidents/INC-2047/publisher-logs.txt` | 10 | `eventType=InvoiceApproved invoiceId=INV-44248` |
| E8 | 2.15.1 swap completed | `ops/incidents/INC-2047/publisher-logs.txt` | 13 | `Swap completed release=2.15.1` |
| E9 | First new-name event | `ops/incidents/INC-2047/publisher-logs.txt` | 15 | `eventType=invoice.approved` |
| E10 | Health check ok, 0 processed | `ops/incidents/INC-2047/worker-logs.txt` | 19-23 | `Health check ok. Messages processed in last 12h: 0` |
| E11 | Alert fired 14:06Z | `ops/incidents/INC-2047/alert.md` | 3 | `2026-01-29 14:06Z` |
| E12 | 2.15.1 contents | `ops/incidents/INC-2047/recent-changes.md` | 15-21 | `dotted lowercase` |
| E13 | Release note warns consumers | `ops/incidents/INC-2047/recent-changes.md` | 25-26 | `Consumers filtering on the old PascalCase names must` |
| E14 | No subscription changes | `ops/incidents/INC-2047/recent-changes.md` | 36 | `No change was made to any subscription` |
| E15 | Worker filter on old name | `ops/incidents/INC-2047/servicebus-status.txt` | 19-26 | `"eventType": "InvoiceApproved"` |
| E16 | notification-worker 0 active, 0 dead | `ops/incidents/INC-2047/servicebus-status.txt` | 15 | `notification-worker    0         0` |
| E17 | No restart, no errors | `ops/incidents/INC-2047/worker-logs.txt` | 25-26 | `No ERROR entries` |
| E18 | Named approver; IC out of hours | `ops/incidents/INC-2047/README.md` | 28-29 | `named approver` |
| E19 | Rollback affects whole release | `ops/incidents/INC-2047/README.md` | 30-31 | `A rollback affects everything else` |
| E20 | Recovery is separate from restoring flow | `ops/incidents/INC-2047/README.md` | 32-33 | `not the same as recovering what was lost` |
| E21 | Topic message TTL 14 days | `ops/incidents/INC-2047/servicebus-status.txt` | 48 | `"ttl": "P14D"` |
