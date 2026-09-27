# Implementation Plan: Retrieve a Payment's Details (UC2)

**Branch**: `develop` (spec directory `specs/002-retrieve-payment`) | **Date**: 2026-09-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/002-retrieve-payment/spec.md`

## Summary

A merchant retrieves a payment with `GET /api/payments/{id}`. The id is bound as a `Guid` with
**no route constraint**, so every GUID notation the platform parses is accepted in any letter
case, and anything else becomes a **`400` invalid id** naming `id` – never a `404`. The concrete
`RetrievePaymentService` asks the `IPaymentRepository` port (new `GetById`) and returns
`Found(Payment)` or `NotFound`. `PaymentResultMapper` – still the single result → HTTP translator
– answers `200` with UC1's own `PaymentResponse` built by UC1's mapping (so the retrieved
payment equals the processing response by construction) or a `404` `ProblemDetails`. UC1's
action-aware invalid-model factory gains the retrieval case (fixed `id` message, no
`paymentStatus`). Every
outcome is logged with the trace id; there is no retrieval metric, no bank call and no side
effect.

UC2 **depends on UC1 being implemented** (the hexagon folders, `Payment`, the repository,
`PaymentsController`, `PaymentResultMapper`, the action-aware invalid-model factory,
`UseStatusCodePages`, the framework log-level rule, observability and the test fixtures – all
aligned with constitution 1.0.2). It adds no project, package, configuration key or entity.

Details: [research.md](research.md), [data-model.md](data-model.md),
[contracts/payments-api.yaml](contracts/payments-api.yaml), [quickstart.md](quickstart.md).

## Technical Context

**Language/Version**: C# 12 / .NET 8 (`net8.0`, SDK 8.0.202 installed)

**Primary Dependencies**: ASP.NET Core Web API (controllers): routing, model binding,
`[ApiController]` automatic 400, `ProblemDetails` (+ UC1's `UseStatusCodePages`), `ILogger`
(`LoggerMessage`); Swashbuckle.AspNetCore (from UC1). **No new package** (research R12).

**Storage**: UC1's in-memory `ConcurrentDictionary<Guid, Payment>` behind `IPaymentRepository`,
extended with `GetById` (read only)

**Testing**: xUnit, hand-written fakes, `WebApplicationFactory<Program>`, WireMock.Net (bank, for
the `POST` that precedes each `GET`), `FakeLogger`; E2E against the Mountebank simulator

**Target Platform**: Linux container (UC1's image) and `dotnet run`

**Project Type**: web service (REST API) – hexagonal, single project

**Performance Goals**: SC-004 – 95 % of retrievals < 500 ms (in-memory lookup, no I/O, no bank
call)

**Constraints**: no side effects; never contacts the bank; malformed id → `400`, never `404`;
`400` for an invalid id without `paymentStatus`; the raw id is never echoed or logged; no PAN/CVV
anywhere; zero build warnings

**Scale/Scope**: one new endpoint; concurrent retrievals and processing must be safe; data lost on
restart (accepted)

No `NEEDS CLARIFICATION` remains – the spec's plan notes (whitespace, empty id, `404` vs `400`,
`paymentStatus` branching, concurrency) are resolved in research R1, R4, R5 and R7.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design, and re-run against
constitution **1.0.2**.*

| # | Principle | Gate | Result | Evidence |
|---|---|---|---|---|
| I | Requirements Fidelity | FRs trace to the assessment; nothing unrequested (no listing, search, auth, metric) | ✅ | spec FRs cite sources; Q5 declined a retrieval metric |
| II | Hexagonal Architecture | GET action in `Http/`; `RetrievePaymentService` in `Application/`; `GetById` on the driven port; adapter in `Infrastructure/`; `Program.cs` only composition root; no `IRetrievePaymentUseCase` | ✅ | data-model; research R6, R7 |
| III | Use-Case-Driven | UC2 = one feature, one concrete service; tasks labelled `[UC2]`; builds on UC1 without restructuring | ✅ | UC1 plan "Structure Decision" anticipated exactly these additions |
| IV | Test-First | Tests precede each implementation task in `tasks.md` | ✅ | enforced in `/speckit-tasks` |
| V | Behaviour-Focused Testing | Unit (service with `FakePaymentRepository`, repository add-then-get); Integration through the real pipeline; E2E process-then-retrieve tagged `E2E`; `[Theory]` for accepted and invalid id forms; deterministic (no concurrency stress test) | ✅ | research R7, R11 |
| VI | Clean Code & SOLID | `NotFound` is a result case, not an exception or leaking `null`; named constants for messages; the port grows by one method its consumer needs | ✅ | research R3, R6; data-model "Constants" |
| VII | Simplicity | No new project or package; response type and mapping reused; no custom model binder; routing-error format and log-level rule reused from UC1 | ✅ | research R2, R5, R12 |
| VIII | Card Data Protection | Only last four digits returned; invalid-id message fixed and raw id never logged; no HTTP logging, `Microsoft.AspNetCore` and `System.Net.Http.HttpClient` at `Warning` (UC1, 1.0.2); UC1's card-number-in-path/body log test plus UC2's card-like invalid id test | ✅ | research R8, R9 |
| IX | Input Validation | The route id is validated before use, fail-closed; every GUID notation interprets the same value (Q2), anything else refused; `[Theory]` boundaries | ✅ | research R1, R4 |
| X | Documentation | XML docs + `[ProducesResponseType]` on the GET action; OpenAPI lists 200/400/404/500; README and `.http` updated | ✅ | research R10 |
| XI | Observability | One log entry per retrieval outcome with `traceId`; `traceId` in every `ProblemDetails`; no metric (Q5 – Principle XI requires none for retrieval) | ✅ | research R8 |
| XII | Packaging & Hosting | No change to Dockerfile, compose, configuration or CI | ✅ | – |
| – | API Design constraints (1.0.2) | `GET /api/payments/{id}` → `200` / `404` / `400` (malformed id, `errors.id`, no `paymentStatus`); `paymentStatus` only on `POST /api/payments` `400`s; every error `ProblemDetails` with `traceId`, including the routing `405` for an empty id | ✅ | research R3, R4, R5; contract |

**Result**: PASS – no violations. The API Design deviation recorded in the previous revision is
resolved by constitution 1.0.2; Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/002-retrieve-payment/
├── spec.md
├── plan.md              # this file
├── research.md          # Phase 0 (R1–R12)
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   └── payments-api.yaml  # retrieval operation; $refs UC1's schemas
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks – not created here)
```

### Source Code (repository root)

Only files added (**+**) or changed (**~**) by UC2; everything else is as delivered by UC1.

```text
README.md                                 ~ API usage, Design Decisions, Observability, Test strategy,
                                          #   Production next steps (research R10)
src/PaymentGateway.Api/
├── PaymentGateway.Api.http               ~ process-then-retrieve, other notations, not found, invalid id
├── Program.cs                            ~ register RetrievePaymentService
├── Application/
│   ├── IPaymentRepository.cs             ~ + GetById(Guid) → Payment? (XML-documented)
│   ├── RetrievePaymentService.cs         + UC2 application service (concrete); logs 3000/3001
│   └── RetrievePaymentResult.cs          + Found(Payment) | NotFound
├── Infrastructure/
│   └── InMemoryPaymentRepository.cs      ~ GetById via TryGetValue
└── Http/
    ├── PaymentsController.cs             ~ + GET {id} (Guid, no :guid constraint; XML docs;
    │                                     #   ProducesResponseType 200/400/404/500)
    ├── PaymentResultMapper.cs            ~ Found → 200, NotFound → 404; + the invalid-id body
    └── UnreadableRequestHandler.cs       ~ invalid-model response on the retrieval action → invalid
                                          #   id (fixed message, no paymentStatus) + log
                                          #   PaymentIdInvalid (3002)

test/PaymentGateway.Api.Tests/
├── Unit/
│   ├── Application/RetrievePaymentServiceTests.cs      +
│   ├── Infrastructure/InMemoryPaymentRepositoryTests.cs +
│   └── Fakes/FakePaymentRepository.cs                  ~ + GetById
├── Integration/
│   └── RetrievePaymentEndpointTests.cs                 +
└── EndToEnd/
    └── RetrievePaymentJourneyTests.cs                  + [Trait("Category","E2E")]
```

**Structure Decision**: no restructuring – one production project and one test project
(Constitution II, V, VII). UC2 fills the slots UC1's plan reserved: `RetrievePaymentService`,
`IPaymentRepository.GetById`, a `GET` action and its tests.

## Test strategy (UC2 additions)

| Level | Folder | Proves in UC2 | Risks covered |
|---|---|---|---|
| Unit | `Unit/` | service returns `Found` with the recorded payment unchanged or `NotFound`; logs 3000/3001 with `paymentId`; repository add-then-get and unknown id | a not-found turned into an error; a retrieval that alters data |
| Integration | `Integration/` | `GET` after `POST` returns a body **equal** to the `POST` body (Authorized, Declined, leading zeros); every accepted notation finds the same payment; repeated `GET` identical; **no** request reaches WireMock during `GET`; `404` and `400` shapes with `traceId` matching the logs; `400` has no `paymentStatus` while the processing `400` still has it; `405` for an empty id; no PAN, CVV or raw invalid id in responses or logs | malformed id reported as not found; card data leakage through the path; drift between processing and retrieval representations |
| E2E | `EndToEnd/` | process then retrieve against the real simulator (Authorized and Declined) | the merchant journey as a whole (deferred from UC1) |

Concurrency (FR-012) is guaranteed by design – immutable `Payment` stored in a
`ConcurrentDictionary` – and checked in review, not by a non-deterministic stress test
(research R7). Latency (SC-004) is observed in the quickstart, not load-tested (research R11).

## README changes (Principle X)

| # | Section | UC2 content |
|---|---|---|
| 1 | Overview | merchants can retrieve Authorized/Declined payments by id |
| 5 | Test strategy | UC2 rows above; the process-then-retrieve E2E journey |
| 6 | Architecture | retrieval flow added to the sequence diagram |
| 7 | API usage | `GET /api/payments/{id}` examples for `200`, `404`, `400` |
| 8 | Observability | events 3000–3002; `traceId` correlation; no retrieval metric |
| 9 | Design Decisions & Assumptions | accepted GUID notations, case and whitespace; malformed id `400` (not `404`) without `paymentStatus`; empty id `405`; one payment representation (`PaymentResponse`); last four digits instead of a "masked card number" (assessment prose vs table); no ownership check; payments lost on restart |
| 10 | Production next steps | merchant-scoped access to payments; identifier-enumeration monitoring |

## Revision – alignment with constitution 1.0.2 (2026-09-26)

- API Design now lists the retrieval `400`: the Constitution Check deviation and its Complexity
  Tracking entry are removed.
- UC1 now owns the action-aware invalid-model factory, `UseStatusCodePages()`, the framework
  log-level rule and the card-number-in-path/body log test; UC2's `Program.cs` change is reduced
  to registering `RetrievePaymentService`, and the UC1 regression test row is dropped (UC1 covers
  it).
- `PostPaymentResponse` was renamed `PaymentResponse` in UC1's artifacts; UC2 references follow.
- The "Observations for UC1 artifacts" list is removed: every item is resolved in UC1's plan,
  research (R3, R5, R15), data model, contract and quickstart.

## Complexity Tracking

No constitution violations – nothing to justify.
