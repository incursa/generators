# Testing Operating Model

Date: 2026-03-08

## Purpose

This repository now uses a four-lane quality model so fast critical checks, required validation, visible known issues, and broader confidence evidence are no longer mixed into one undifferentiated `dotnet test` pass.

The lane split is intentionally pragmatic:

- smoke is the smallest deterministic slice that should fail fast
- blocking is the required CI-safe validation lane
- observational is reserved for runnable known-issue tests and stays non-blocking
- advisory is broader than blocking and collects coverage plus evidence artifacts

## Current lane definitions

### Smoke

Smoke is driven by tests tagged with `Category=Smoke` and currently covers:

- DTO validation failures in the Roslyn generator
- happy-path parsing for the application-definition pipeline
- deterministic emitter snapshots
- CLI write-mode generation
- check-mode drift detection
- tool packaging metadata

Primary entrypoints:

- runsettings: `runsettings/smoke.runsettings`
- script: `scripts/run-smoke-tests.ps1`

### Blocking

Blocking is the required lane for merges and releases.

It excludes:

- `ExtendedTest`
- `Explicit`
- `KnownIssue`

It also performs a repository-specific drift check before tests:

- `dotnet run --project src/Incursa.Generators.Tool/Incursa.Generators.Tool.csproj -- generate --config examples/AppDefinitions/app-definitions.json --check`

Primary entrypoints:

- runsettings: `runsettings/blocking.runsettings`
- script: `scripts/run-blocking-tests.ps1`

### Observational

Observational is reserved for tests tagged with `Category=KnownIssue`.

Policy:

- the lane is visible in CI
- the lane is non-blocking
- the lane keeps writing results even when no known-issue tests are currently registered

Primary entrypoints:

- runsettings: `runsettings/observational.runsettings`
- script: `scripts/run-observational-tests.ps1`

Current state:

- there are no `KnownIssue` tests registered in this repository today

### Advisory

Advisory broadens confidence beyond blocking without controlling merge eligibility directly.

The current advisory lane includes:

- the full blocking suites with coverage enabled
- the `ExtendedTest` string-backed enum performance suite

Primary entrypoints:

- script: `scripts/run-advisory-quality-tests.ps1`
- evidence bundle: `scripts/run-quality-evidence.ps1`

Artifacts:

- `TestResults/smoke/`
- `TestResults/blocking/`
- `TestResults/observational/`
- `TestResults/advisory/`
- `artifacts/quality/testing/`

## Taxonomy rules

Use `Smoke` for fast high-signal checks that answer whether a major path regressed.

Use `KnownIssue` only when:

- the test is runnable in automation
- the failing behavior is a real product or tooling gap
- the failure is worth keeping visible until fixed

Do not use `KnownIssue` for:

- flaky tests
- broken harnesses
- manual-only scenarios
- missing credentials or environment setup

Use `ExtendedTest` for deterministic but heavier suites that add confidence while staying out of the required fast lane.

## Local commands

Smoke:

```powershell
./scripts/run-smoke-tests.ps1
```

Blocking:

```powershell
./scripts/run-blocking-tests.ps1
```

Observational:

```powershell
./scripts/run-observational-tests.ps1
```

Advisory:

```powershell
./scripts/run-advisory-quality-tests.ps1
./scripts/run-quality-evidence.ps1
```

## Workflow policy

CI now follows this topology:

1. smoke runs first and must pass
2. blocking, observational, and advisory fan out after smoke
3. packaging or publish steps depend on blocking
4. observational and advisory stay non-blocking in GitHub Actions

## Intentional gaps

This pass does not try to solve:

- downstream consumer-project integration for the NuGet packages
- external repository validation of the deterministic CLI tool
- any future known-issue backlog that may be added to the observational lane later

Those gaps are tracked in the quality contract and can be expanded without changing the lane model.
