# Known-Issue Tests

Date: 2026-03-08

## Current list

There are no tests tagged with `Category=KnownIssue` in this repository today.

That is intentional. The observational lane is in place so real known gaps can stay visible without polluting the blocking lane, but no current test has been classified that way yet.

## When to add one

Add `Category=KnownIssue` only when all of the following are true:

- the test runs in automation
- the failing behavior is real
- the gap matters enough to keep visible in CI
- fixing it requires product or non-trivial follow-up work

## When not to add one

Do not use `KnownIssue` for:

- flaky tests
- broken fixtures or broken assertions
- manual-only scenarios
- secret-gated or environment-gated checks

When a known issue is fixed, remove the `KnownIssue` categorization so the test returns to the blocking lane.
