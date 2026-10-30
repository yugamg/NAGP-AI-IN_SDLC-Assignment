# Review eval results

**Date:** 2026-10-29.

**What was run:**
- The 10 fixed cases plus 2 of our own (case 11 with a defect, case 12 clean).
- A baseline of three passes, then three passes with one change.
- 72 reviews in total, one fresh session each.

**Raw outputs** are in [`runs/`](runs/), the per-row scores in
[`results.csv`](results.csv), and the key, written before any run, in
[`answer-key.md`](answer-key.md).

## Set-up

| | Baseline | Changed run |
| --- | --- | --- |
| Prompt | `v1`, the README prompt word for word | same |
| Model | Sonnet (Claude Code subagent alias `sonnet`) | **Haiku** (alias `haiku`): the one change |
| Context | each review a new subagent, told to read only its case file | same |
| Runs | 3 × 12 cases | 3 × 12 cases |

**Why the model, not the context file.** The planned change was the context
file: add `AGENTS.md`. Runs went through subagents because the standalone CLI
was not signed in. A subagent loads the project's `CLAUDE.md`, which pulls in
`AGENTS.md`. So both arms already had it: 25 of the 72 reviews name `AGENTS.md`, and 51
mention it or "the project conventions". With the context file fixed by the
setup, the model was the one variable left that could be changed cleanly.

## Results

**Headline,** scored on the cases with a definite key:
- **Defect cases:** 01, 02, 05, 07, 08, 10, 11.
- **Clean cases:** 04, 06, 12.

| | Sonnet run 1 | run 2 | run 3 | **Sonnet total** | Haiku run 1 | run 2 | run 3 | **Haiku total** |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Caught | 7/7 | 7/7 | 7/7 | **21/21 (100%)** | 7/7 | 7/7 | 7/7 | **21/21 (100%)** |
| Missed | 0 | 0 | 0 | **0/21 (0%)** | 0 | 0 | 0 | **0/21 (0%)** |
| False alarm | 0/3 | 0/3 | 0/3 | **0/9 (0%)** | 0/3 | 0/3 | 0/3 | **0/9 (0%)** |

**The 10 fixed cases on their own:**

| | Sonnet | Haiku |
| --- | --- | --- |
| Caught | 18/18 | 18/18 |
| False alarm | 0/6 | 0/6 |

**Ambiguous cases,** reported separately:

| Case | Sonnet | Haiku |
| --- | --- | --- |
| 03 (rounding change) | Flagged as a defect 3/3, high confidence | Same, 3/3 |
| 09 (false README claim) | Flagged the reset claim 3/3, medium (60–65%) | Flagged it 3/3, about 75% |

**Secondary measure: noise.** Haiku made about 24% more statements per
review: 4.8 "confidence" lines against 3.9. The extra lines were mostly
low-confidence or conditional points, such as "no tests in this diff",
"`_logger` may not exist" and hunk-header mismatches. That is extra reading
for whoever triages the review, but under the README's rule none of it is a
false alarm.

## Was the change an improvement?

**No measurable difference on the three rates.** Both models caught every
defect in every run and raised no false alarm. The only difference observed is
that Haiku is wordier, and it is cheaper per call. Its cost was not measured
here.

**How confident we are: not very.** The set cannot tell these two models
apart.

- **The trials are not independent.** Three runs of the same case are close to
  the same experiment, so the useful sample is closer to 7 defect cases and 3
  clean cases than to 21 and 9.
- **95% Wilson intervals, counting trials:**
  - catch rate 84.5%–100% for each model;
  - false-alarm rate 0%–30% for each model.
- **The same intervals, counting distinct cases:**
  - catch rate 64.6%–100%;
  - false-alarm rate 0%–56%.
- **What that means.** A true false-alarm rate of 1 in 3 would still be
  consistent with what we saw. A one-case difference between the models would
  not be detectable either.
- **There is a ceiling effect.** Every defect in this set is a well-known
  pattern: SQL injection, a missing auth attribute, catch-all success, a new
  `HttpClient` per call. Both models score 100%, so the set measures nothing
  above "competent".

**To make this eval able to show a difference:**
- **Add harder defects** that need context beyond the diff, for example:
  - the 409 that the service only throws because of a status check in
    another file;
  - a contract rename that breaks a subscriber, as in INC-2047.
- **Add at least as many clean cases as defect cases.** Three clean cases
  cannot measure a false-alarm rate.
- **Run each arm 5 or more times.**

## Scoring rules applied

- **Caught:** the review names the defect in the key, as the main finding or
  one of the findings, at the right place, for the right reason. Every catch
  in this run was a high-confidence finding. None depended on generous
  reading.
- **False alarm:** the review *asserts* a defect that is not there.
  - Conditional points ("if the service throws a derived type…", "depends on
    code outside the diff") are not assertions, and nor are style or "no tests
    in this diff" notes.
  - True statements about the diff file are not false alarms either. The
    hunk-header mismatches are real: 8 of the 12 case files (02, 03, 04, 06,
    07, 08, 10 and our own 11) have headers that do not match their bodies.
- **Clean-case borderline: all six case-12 reviews.** Each said "no
  functional defect" and then listed coverage gaps in the test.
  - We checked those gaps, and they are real: a Domain project switched to the
    Web SDK, `<Reference>` items, or references from `Directory.Build.props`
    would all pass both tests.
  - Because the gaps are real, they are not false alarms. They are a finding
    against our own Task 1 test.

## Weaknesses of this run

1. **Isolation is weaker than the README asks.**
   - Subagents load `CLAUDE.md` and `AGENTS.md`.
   - They had tools, and were only *told* to read one file.
   - Every run made 3 or 4 tool calls. The transcripts were not audited to
     prove that no other file was read.
   - The answer key was moved out of the repository during the runs, so it
     could not have been read.
2. **The scorer is not independent.**
   - The same agent that wrote the key scored the reviews, and wrote cases 11
     and 12.
   - The key was fixed before any run. Even so, a second human scorer should
     re-score at least the clean and ambiguous rows.
3. **Exact model versions are not recorded.** The subagent aliases `sonnet` and
   `haiku` do not record a version.
4. **Our own case files have malformed hunk headers,** case 11 in particular.
   The reviewers noticed, so this cost attention rather than accuracy.

## Our two cases

| Case | Kind | Source | Result |
| --- | --- | --- | --- |
| [case-11](cases/case-11.diff) | Defect: a new `HttpClient` per call, and the timeout dropped | The INC-2041 release diff. The incident logs show the socket errors in production | Caught 6/6, both models, high confidence |
| [case-12](cases/case-12.diff) | Clean: the Task 1 architecture test | Its correctness was measured with seeded violations | 0/6 false alarms. Several reviews found real coverage gaps we had missed. Those gaps are now closed in the real test; `case-12.diff` stays a frozen snapshot of the version that was reviewed |
