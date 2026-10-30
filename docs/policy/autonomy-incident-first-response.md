# Autonomy policy: first-response incident summaries

**Workflow.** In the first 30 minutes after a page, an agent reads the
incident evidence and drafts the first summary for the incident channel:
- what is affected;
- the timeline;
- what changed recently;
- ranked hypotheses;
- options for the incident commander (IC).

**Owner:** senior engineer, Invoice Approval. **Status:** proposed, 2026-10-29.
**Worked example:** [INC-2047 at level 1](examples/INC-2047-first-response-L1.md).

## Levels

| Level | The agent may | It may not |
| --- | --- | --- |
| **L0 Manual** | Nothing | |
| **L1 Draft from exports** | Read evidence the on-call engineer exports into the incident folder, and draft the summary in the template | Touch any live system. Post anything. |
| **L2 Draft with live reads** | Also run allow-listed read-only queries itself (log queries; `az … show / list`; Service Bus peek through a wrapper) | Post. Run anything outside the allow-list. |
| **L3 Auto-post first draft** | Post the draft to the incident channel only, labelled *"AI draft, not yet reviewed"* | Post anywhere else, page, or escalate |
| **Never, at any level** | | Change production. Message finance, suppliers or the status page. State a root cause as fact. |

## Tasks and their ceilings

| Task | Ceiling | Why it stops there |
| --- | --- | --- |
| Timeline from logs and deploy records | L3 | Mechanical, and every line can be checked against its source |
| Recent-change correlation (releases, config) | L3 | Same. In INC-2047 the release note itself warned consumers |
| Impact count (invoices, suppliers affected) | L2 | Finance acts on the number. In INC-2047 three sources gave three different counts |
| Ranked hypotheses, each with what would disprove it | L2 | The agent may draft them. A human decides which one leads, and no hypothesis is posted unreviewed |
| Mitigation options for the IC | L1 | Each option must state what it changes, how it is undone, and its blast radius. The IC decides |
| Updates to finance or suppliers, status page | L0 | Outward-facing, and costly if wrong |

## Controls that make each level safe

**At L1 and above:**

1. **Citations are checked.** Every factual line cites `file:line`, and
   `check_citations.py` must pass before the draft is shown. A claim that
   comes from how a product behaves, not from the evidence, is marked
   *(general knowledge, verify)*. Writing the INC-2047 example showed this
   rule was needed.
2. **Health claims need a number.** A claim about health must cite a metric,
   never a status word. In INC-2047 the worker logged `Health check ok`
   while it processed 0 messages.
3. **Hypotheses are labelled.** They sit under their own heading, ranked,
   each with "what would disprove it". The word "root cause" is not allowed
   in a draft.
4. **Personal data is redacted.** Supplier e-mail addresses, approver names
   and amounts are redacted before posting. The worker logs contain
   recipient addresses.
5. **The draft records its reviewer.** It says who reviewed it, and the
   prompt and model are stored in the incident record.

**At L2, also:**

6. **Credentials are enforced, not prompted.**
   - The agent's identity has Reader roles only.
   - Its tools are an allow-list enforced by a wrapper, not by the prompt.
   - Service Bus peek goes through a wrapper that exposes peek and nothing
     else. The right that allows peek also allows receive and delete *(to
     confirm against Azure's documentation before granting)*.
7. **Every command is logged** with its output in the incident record.

**At L3, also:**

8. **It posts to one channel only.** Any on-call engineer can switch off
   auto-posting with one setting.
9. **An unacknowledged draft is retracted.** The draft must be acknowledged
   by a human within 15 minutes, or it is withdrawn.

## Moving a task up a level

| Move | Evidence needed | Approved by |
| --- | --- | --- |
| L1 → L2 | Wrapper and permissions reviewed by security, plus 10 incidents (real, or replays of `ops/incidents` packs) with: no uncited claim reaching the channel, no missed redaction, and no more than 1 material factual correction per draft on average | Service owner **and** security |
| L2 → L3 | 20 incidents at L2. The draft's leading hypothesis never stated as fact. No blocked command attempts, or each one explained. First draft faster than the human baseline. Every on-call engineer has reviewed at least 2 drafts | Service owner **and** rotation lead |
| Any change of model or prompt | Re-run the INC-2041 and INC-2047 replays and compare with the last accepted drafts | Service owner |

**Moving down needs no approval.**
- Anyone on the rotation can drop a task one level, at once.
- A posted claim that misled the response drops the task one level.
- Any attempted write drops the whole workflow to L0 until it is reviewed.

## Day one of the rotation

**Every task runs at L1. On top of that, for the first two incidents in which
a new engineer is primary, an experienced secondary reviews the draft before
it is posted.**

- **Not L0,** because the citation rule actually helps a new engineer. The
  draft points at the lines that matter in an unfamiliar codebase.
- **Not L2,** because L1's main control is the person reviewing the draft.
  Two of the six engineers cannot yet tell a plausible hypothesis from a
  right one. In INC-2047, "the worker is healthy" reads as true in the logs
  and is wrong.
- **No evidence yet.** Nothing has run at L1 here, so there is nothing to
  justify more.

The first move up is reviewed after the first month-end window, against the
L1 → L2 evidence above.
