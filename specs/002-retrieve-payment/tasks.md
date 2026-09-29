---

description: "Task list for UC2 – Retrieve a Payment's Details"
---

# Tasks: Retrieve a Payment's Details (UC2)

**Input**: Design documents from `specs/002-retrieve-payment/` – [plan.md](plan.md),
[spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/payments-api.yaml](contracts/payments-api.yaml), [quickstart.md](quickstart.md)

**Prerequisites**: constitution 1.1.0 (`.specify/memory/constitution.md`); UC1 implemented
(`specs/001-process-payment/`, all 93 tasks done) – the role folders, `Payment`,
`IPaymentRepository`, `InMemoryPaymentRepository`, `PaymentsController`, `PaymentResultMapper`,
`InvalidModelStateResponder`, `PaymentResponse`, `UseStatusCodePages()`, the framework log-level
rule, `PaymentGatewayFactory` and `FakePaymentRepository` all exist and are aligned with
constitution 1.1.0.

**Naming correction (2026-09-27, revised)**: the invalid-model mechanism was first described as
`Http/UnreadableRequestHandler.cs`, then as `PaymentResultMapper.ToUnreadableBodyResult`. As
built (commit 7dc4c59, constitution 1.1.0), it is split in two:
**`Http/Payments/InvalidModelStateResponder`** – wired as
`ApiBehaviorOptions.InvalidModelStateResponseFactory` in `Program.cs` – reads the target action's
`[RespondsToInvalidRequest(InvalidRequestResponse.…)]` attribute, logs, and asks
**`PaymentResultMapper`** to build the body. The same commit moved every file into role folders
and made the repository port and the use case asynchronous. The tasks below describe the code as
built; `/speckit-analyze` findings D1, I1 and I2.

**Tests**: mandatory (Constitution IV – TDD is non-negotiable). Every behaviour is a
**Red → Green pair**: a test task immediately followed by the implementation task(s) it drives.
A test that does not compile because the type/member it needs does not exist yet counts as
**Red**; the type/member is created in the Green step.

**Organization**: UC2 needs no new Setup or Foundational phase – it reuses UC1's project,
composition root and test fixtures unchanged (Constitution III: "foundational tasks are allowed
only to the extent the use case being delivered needs them"; UC1 tasks.md confirms UC2 "builds on
this list"). Work starts directly at the repository port UC2 adds, then the application service,
then the HTTP endpoint (split per user story – **(US1)** Found, **(US2)** Not found, **(US3)**
Invalid id), then documentation and the E2E journey. All use-case work is labelled **`[UC2]`**
(Constitution III uses use-case IDs instead of `[US1]`); the spec's user story each task serves is
given in brackets in the description.

## Format: `[ID] [P?] [UC2?] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[UC2]**: use-case task (documentation/polish tasks carry no label)
- Each task is one Red-Green-Refactor step and one Conventional Commit (`test:`, `feat:`,
  `refactor:`, `docs:`); the commit type is given at the end of each task.

## Conventions for every test task

- Test names `<Unit>_<Scenario>_<ExpectedBehaviour>`; explicit `// Arrange`, `// Act`,
  `// Assert`; one Act; no conditionals or loops; boundaries and data variations as `[Theory]`
  (Constitution V).
- Unit tests use `test/Unit/Fakes/FakePaymentRepository.cs` (UC1 fake); no network, clock or
  randomness (UC2's service has no `TimeProvider` dependency – research R6).
- Integration tests use `PaymentGatewayFactory` (UC1 T005) – the real pipeline; the bank is
  replaced by WireMock but UC2's `GET` never calls it (FR-010).
- A test task is done when the test **fails for the right reason** (Red); the implementation
  task(s) that follow are done when it passes and all other tests stay green (Green).

Paths are relative to the repository root. `src/` = `src/PaymentGateway.Api/`,
`test/` = `test/PaymentGateway.Api.Tests/`.

---

## Phase 1: Repository read [UC2]

**Goal**: `IPaymentRepository` gains a read method the application service can call
(FR-001, FR-011, research R7).

**Independent test**: `dotnet test --filter "FullyQualifiedName~Unit.Infrastructure.Persistence.InMemoryPaymentRepositoryTests"`.

- [X] T001 [P] [UC2] Write `test/Unit/Infrastructure/Persistence/InMemoryPaymentRepositoryTests.cs`
  (US1): `AddThenGetById_ReturnsTheSamePayment` (add a `Payment`, `GetByIdAsync` its `Id` returns
  the same instance) and `GetById_WhenIdIsUnknown_ReturnsNull`. Red (does not compile:
  `GetByIdAsync` does not exist). `test:`
- [X] T002 [UC2] Add `Task<Payment?> GetByIdAsync(Guid id)` to
  `src/Application/Ports/IPaymentRepository.cs` with an XML `<summary>` ("returns the payment
  recorded under `id`, or `null` when none exists; no side effects; safe to call concurrently with
  `AddAsync`"), and implement it in `src/Infrastructure/Persistence/InMemoryPaymentRepository.cs`
  via `Task.FromResult(_payments.TryGetValue(id, out Payment? payment) ? payment : null)`. Green
  step 1/2 for T001. `feat:`
- [X] T003 [UC2] Implement `GetByIdAsync` in `test/Unit/Fakes/FakePaymentRepository.cs` so the
  fake keeps compiling against the extended `IPaymentRepository`. Green step 2/2 for T001. `feat:`

**Checkpoint**: `dotnet build` succeeds with the extended port; `dotnet test --filter
"FullyQualifiedName~Unit.Infrastructure"` is green.

---

## Phase 2: RetrievePaymentService [UC2]

**Goal**: the use case looks up a payment and returns an explicit result, with no bank dependency
and no metric (research R6, R8; Clarifications Q5).

**Independent test**: `dotnet test --filter "FullyQualifiedName~Unit.Application.RetrievePayment.RetrievePaymentServiceTests"`.

- [X] T004 [UC2] Write `test/Unit/Application/RetrievePayment/RetrievePaymentServiceTests.cs` with
  a `FakeLogger`: (US1) `Retrieve_WhenPaymentExists_ReturnsFoundWithTheRecordedPayment` (add a
  payment to `FakePaymentRepository`, `RetrieveAsync(payment.Id)` returns `Found` carrying the
  same instance) and
  `Retrieve_WhenPaymentExists_LogsPaymentRetrieved` (EventId 3000, Information, `paymentId`,
  `status`); (US2) `Retrieve_WhenPaymentDoesNotExist_ReturnsNotFound` and
  `Retrieve_WhenPaymentDoesNotExist_LogsPaymentNotFound` (EventId 3001, Information, `paymentId` =
  the looked-up id). Red (does not compile: the application types do not exist yet). `test:`
- [X] T005 [UC2] Create `src/Application/RetrievePayment/RetrievePaymentResult.cs`
  (`Found(Payment)` | `NotFound`, same closed-result pattern as `ProcessPaymentResult`) and
  `src/Application/RetrievePayment/RetrievePaymentService.cs` (concrete, no interface – UC1
  research R11; dependencies `IPaymentRepository`, `ILogger<RetrievePaymentService>` only, **no**
  `IAcquiringBank`, **no** `TimeProvider`, **no** `PaymentGatewayMetrics`;
  `RetrieveAsync(Guid id)` awaits `GetByIdAsync` and returns `Found`/`NotFound` – asynchronous
  because the port is, research R6). Green step 1/2 for T004. `feat:`
- [X] T006 [UC2] Add `LoggerMessage` source-generated methods `PaymentRetrieved` (EventId 3000,
  Information, `paymentId`, `status`) and `PaymentNotFound` (EventId 3001, Information,
  `paymentId`) in the partial class `src/Application/RetrievePayment/RetrievePaymentService.Log.cs`,
  and call them from `RetrievePaymentService.RetrieveAsync`. Green step 2/2 for T004. `feat:`

**Checkpoint (Use case)**: `Found` and `NotFound` are proven with the fake repository; each
outcome logs its event; no test constructs an `IAcquiringBank` or `PaymentGatewayMetrics` for this
service (FR-010, Q5).

---

## Phase 3: HTTP endpoint and mapping [UC2]

**Goal**: `GET /api/payments/{id}` translates HTTP ↔ use case through the existing
`PaymentResultMapper` (research R1–R4; data-model "Http"), split per user story.

**Independent test**: `dotnet test --filter "FullyQualifiedName~Integration.RetrievePaymentEndpointTests"`.

### (US1) Merchant retrieves a previously processed payment

- [X] T007 [UC2] Write `test/Integration/RetrievePaymentEndpointTests.cs` (US1), using
  `PaymentGatewayFactory` and WireMock only for the `POST` that precedes each `GET`:
  `Get_AfterProcessing_ReturnsTheSameBodyAsTheProcessingResponse` (`[Theory]` Authorized and
  Declined via WireMock: `POST` then `GET {id}` → `200`, body **equal, field for field**, to the
  `POST` response); `Get_WhenLastFourHaveLeadingZeros_ReturnsThemAsText` (card ending `0012`);
  `[Theory] Get_WithAnyAcceptedGuidNotation_ReturnsTheSamePayment` over the id in uppercase, without
  hyphens (`N`), in braces (`B`, URL-encoded `%7B…%7D`), in parentheses (`P`) and in the
  hexadecimal form (`X`, added by T025) → `200`, `id` in the body is the canonical lowercase form
  (surrounding whitespace was in this list until C1 – now refused, T020); `Get_CalledTwice_ReturnsTwoIdenticalBodies` (FR-009); and `Get_ForARecordedPayment_MakesNoRequestToTheBank`
  (`_bank.Server` receives exactly the one `POST` request and none for the `GET`s, FR-010). Red
  (does not compile: no `GET` action exists). `test:`
- [X] T008 [UC2] Green step 1/2 for T007: add the `GET` action to
  `src/Http/Payments/PaymentsController.cs`: `[HttpGet("{id}")] public async Task<IActionResult>
  RetrievePaymentAsync(Guid id)` – route has **no `:guid` constraint** (research R1);
  asynchronous because the port is (research R6); constructor now also takes
  `RetrievePaymentService`; the action awaits `_retrievePaymentService.RetrieveAsync(id)` then
  calls `_mapper.ToActionResult(result, HttpContext)`. `feat:`
- [X] T009 [UC2] Green step 2/2 for T007: add an overload
  `IActionResult ToActionResult(RetrievePaymentResult result, HttpContext httpContext)` to
  `src/Http/Payments/PaymentResultMapper.cs`: `Found(payment)` → `200` + `PaymentResponse.From(payment)`
  (the same mapping UC1 uses, so the body equals the processing response by construction – FR-003,
  research R2); `NotFound` → `404` via the existing `Problem(...)` helper with a `ProblemDetails`
  (`Type` = `https://tools.ietf.org/html/rfc9110#section-15.5.5`, `Title` = `"Payment not
  found"`, `Detail` = `"No payment exists with the given id."`, `Status` = 404) – the id is never
  echoed (FR-008, research R3). Register `RetrievePaymentService` in `src/Program.cs`
  (`builder.Services.AddScoped<RetrievePaymentService>();`, next to `ProcessPaymentService`).
  Green for T007. `feat:`

**Checkpoint (US1)**: quickstart scenarios 1–5 work: `GET` after `POST` returns an identical body,
leading zeros survive, every accepted notation finds the same payment, repeated `GET`s are
identical, and WireMock sees no request during `GET`.

### (US2) Merchant is told when a payment cannot be found

- [X] T010 [UC2] Add to `RetrievePaymentEndpointTests` (US2) `[Theory]`
  `Get_WhenIdIsWellFormedButUnknown_Returns404` over the all-zeros id
  (`00000000-0000-0000-0000-000000000000`) → `404`, `application/problem+json`, `title: "Payment
  not found"`, `detail: "No payment exists with the given id."`, `traceId`, no payment field in the
  body; `Get_WhenIdIsUnknown_ResponseIsIdenticalForEveryId` (two fresh random GUIDs produce
  bodies identical apart from `traceId`, FR-008 – strengthened by T027 to compare the whole
  body); and `Get_WhenIdIsUnknown_TraceIdCorrelatesWithThePaymentNotFoundLogEntry` (the body's
  `traceId` is a 32-hex id and equals the `TraceId` scope value of the `PaymentNotFound` log
  entry – `FakeLogger` via `factory.LogCollector`; the format check was added by T026). Expected green immediately after T009 (the mapping already exists);
  if any assertion fails, treat it as Red and close the gap. `test:`

**Checkpoint (US2)**: quickstart scenarios 6–7 work: an unknown or all-zeros id, and a payment
looked up after a restart (fresh factory instance), both answer `404` with the correlated
`traceId`.

### (US3) Merchant is told when the identifier is not valid

- [X] T011 [UC2] Add to `RetrievePaymentEndpointTests` (US3) `[Theory]`
  `Get_WhenIdIsNotAGuid_Returns400NamingTheIdField` over `abc`, `123`, a 35-character string, a
  37-character string, a value containing `g`, a canonical value with a misplaced hyphen, a braced
  value with a mismatched bracket, a whitespace-only value, and the card-like value
  `4111111111111111` → `400`, `application/problem+json`, `title: "Invalid payment id"`,
  `errors: { "id": ["The payment id must be a GUID, e.g.
  3fa85f64-5717-4562-b3fc-2c963f66afa6."] }`, **no** `paymentStatus` member, `traceId`; none of the
  submitted values appears in the response body. Add
  `Get_WhenIdIsInvalid_LogsPaymentIdInvalidWithoutTheRawValue` (`PaymentIdInvalid`, EventId 3002,
  Information, no field carries the raw id) and
  `Get_WhenIdIsInvalid_NeitherPaymentRetrievedNorPaymentNotFoundIsLogged` (the application service
  is never reached, so the store is not searched – FR-005). Add
  `Get_OnTheCollectionRoute_Returns405` (`GET /api/payments` and `GET /api/payments/` → `405`
  `ProblemDetails` with `traceId`, delivered by UC1's `UseStatusCodePages()` – research R5; no new
  code). Also add `Respond_ForInvalidPaymentIdAction_ReturnsInvalidIdWithoutEchoingIt` to
  `test/Unit/Http/Payments/InvalidModelStateResponderTests.cs` (an `ActionContext` marked
  `InvalidRequestResponse.InvalidPaymentId` → `400` "Invalid payment id", `errors.id` only, no
  `paymentStatus`, the attempted value absent). Red for the `400` and logging assertions: today every action without
  `[RespondsToInvalidRequest]` gets a generic `ValidationProblemDetails` with the framework's
  default message, which echoes the submitted value, and no 3002 log entry is written. `test:`
- [X] T012 [UC2] In `src/Http/Payments/`: add `InvalidRequestResponse.InvalidPaymentId`; decorate
  `PaymentsController.RetrievePaymentAsync` with
  `[RespondsToInvalidRequest(InvalidRequestResponse.InvalidPaymentId)]`; in
  `InvalidModelStateResponder.Respond` add the `InvalidPaymentId` case, which logs
  `PaymentIdInvalid` (new `LoggerMessage` in `InvalidModelStateResponder.Log.cs`, EventId 3002,
  Information, no parameters – the raw id is never passed to the logger) and returns
  `PaymentResultMapper.InvalidPaymentId(httpContext)`. In `PaymentResultMapper`, add constants
  `InvalidIdTitle = "Invalid payment id"`, `InvalidIdField = "id"`, `InvalidIdMessage = "The
  payment id must be a GUID, e.g. 3fa85f64-5717-4562-b3fc-2c963f66afa6."` and
  `InvalidPaymentId(HttpContext)`, building a `ValidationProblemDetails` (`Status = 400`, **no**
  `paymentStatus`) via the existing `Problem(...)` helper. Actions without the attribute keep the
  plain `ValidationProblem` fallback. Green for T011. `feat:`

**Checkpoint (US3)**: quickstart scenarios 8–10 work: a malformed id (including a card-like value)
returns `400` naming `id` with a fixed message and is logged as `PaymentIdInvalid`; the empty-id
route returns `405`.

---

## Phase 4: Card data protection guard [UC2]

**Goal**: prove, for the retrieval path specifically, that no PAN or CVV reaches a log or a
response (Constitution VIII 1.0.2; research R9).

- [X] T013 [UC2] Add to `test/Integration/CardDataLoggingTests.cs`:
  `Get_ForAProcessedPayment_LogsNoPanOrCvv` (process a payment with a distinct card number and CVV
  via WireMock, `GET` it, assert no collected log entry from the `GET` contains either value –
  guard: `Payment`/`PaymentResponse` never held them, so this proves the retrieval path adds no
  leak) and `Get_WithACardLikeInvalidId_IsNotLoggedOrReturned` (`GET
  /api/payments/4111111111111111` → neither the response body nor any collected log entry contains
  `4111111111111111`). Expected green on first run (guard, following UC1 T073's pattern); prove it
  can fail by temporarily logging the raw route value in `InvalidModelStateResponder`'s
  `InvalidPaymentId` case (red),
  then revert. **Verify**: both runs observed. `test:`

---

## Phase 5: API documentation [UC2]

**Goal**: the OpenAPI document and README describe the retrieval endpoint (FR-017; Constitution X;
research R10).

- ~~T014~~ [UC2] **Dropped** (not done, no commit): ~~Add an OpenAPI-shape test to
  `OpenApiDocumentTests.cs`~~ – a test asserting the generated Swagger JSON's `$ref` values would
  exercise Swashbuckle's own behaviour, not this codebase's. Documentation completeness (FR-017)
  is verified manually instead, via Swagger UI and the quickstart (T019). The one OpenAPI fact
  this codebase does control – `id` is a path parameter – is tested by T022.
- [X] T015 [UC2] Add an XML `<summary>`/`<remarks>`/`<param>`/`<response>` block to
  `PaymentsController.RetrievePaymentAsync` (accepted GUID forms, letter case, whitespace; the three
  outcomes) and `[ProducesResponseType]` for `200` (`PaymentResponse`), `400`
  (`ValidationProblemDetails`), `404` (`ProblemDetails`), `500` (`ProblemDetails`). Verified
  manually: `GET /swagger/v1/swagger.json` lists `/api/payments/{id}` with all four responses and a
  non-empty `id` parameter description. `docs:`
- [X] T016 [P] [UC2] Add to `src/PaymentGateway.Api.http`: a named `POST` request followed by a
  `GET {{postPayment.response.body.$.id}}` (process-then-retrieve), a `GET` using the id in
  uppercase/without hyphens/in braces, a `GET` for the all-zeros id (not found), and a `GET
  /api/payments/abc` (invalid id). **Verify**: each returns the status documented in
  [quickstart.md](quickstart.md) §2. `docs:`
- [X] T017 [UC2] Update `README.md`: Overview (merchants can retrieve Authorized/Declined payments
  by id); API usage (`GET /api/payments/{id}` examples for `200`, `404`, `400`); Observability
  (events 3000 `PaymentRetrieved`, 3001 `PaymentNotFound`, 3002 `PaymentIdInvalid`; no retrieval
  metric); Test strategy (the rows of this feature); Design Decisions & Assumptions (accepted GUID
  notations/case; whitespace refused – T024; malformed id → `400` without `paymentStatus`, never `404`; empty id →
  `405`; one payment representation, `PaymentResponse`, reused unchanged; last four digits instead
  of a "masked card number"; no ownership check; payments lost on restart); Production next steps
  (merchant-scoped access, identifier-enumeration monitoring). `docs:`

---

## Phase 6: End-to-end journey [UC2]

- [X] T018 [UC2] Write `test/EndToEnd/RetrievePaymentJourneyTests.cs` with
  `[Trait("Category", "E2E")]`, reusing `SimulatorGatewayFactory` (UC1 T081): `[Theory]` process a
  payment against the real simulator (card ending `7` → Authorized, ending `8` → Declined,
  12/2030) then `GET` its id → the retrieved body equals the processing response. **Verify**:
  `docker compose up -d bank_simulator` then `dotnet test --filter "Category=E2E"` passes, and
  `dotnet test --filter "Category!=E2E"` does not run it. `test:`

---

## Phase 7: Definition of Done

- [X] T019 Run the Definition of Done: `dotnet build -c Release` (0 warnings),
  `dotnet format --verify-no-changes`, `dotnet test --filter "Category!=E2E" --collect:"XPlat Code
  Coverage"`, the E2E run, and every scenario of [quickstart.md](quickstart.md) §1–§4 including the
  manual latency check of §3 (SC-004); fix any gap in the task that introduced it. **Verify**: all
  gates pass. `chore:`

---

## Phase 8: Surrounding whitespace refused [UC2] (`/speckit-analyze` finding C1)

**Goal**: an id with leading or trailing whitespace is refused as invalid, never trimmed into
validity (Constitution IX; spec Edge Cases; research R1, revised 2026-09-27).

- [X] T020 [UC2] (US3) In `test/Integration/RetrievePaymentEndpointTests.cs`, remove the
  `%20{id}%20` case from `AcceptedNotations` and add to
  `Get_WhenIdIsNotAGuid_Returns400NamingTheIdField` a valid GUID with surrounding spaces, with a
  trailing space, and with a leading tab. Red: each returns `404` – the default `Guid` binder
  trims the value into a valid (unknown) GUID and looks it up. `test:`
- [X] T021 [UC2] Add `src/Http/Payments/PaymentIdModelBinder.cs`: reads the `id` route value as
  sent; binds it only when it has no surrounding whitespace and `Guid.TryParse` accepts it;
  otherwise adds a model error on `id` (message `PaymentResultMapper.InvalidIdMessage`, now
  public), so `[ApiController]`'s automatic `400` → `InvalidModelStateResponder` → the existing
  invalid-id response. Apply it to `RetrievePaymentAsync`'s `id` parameter; update the action's
  XML `<remarks>` ("surrounding whitespace is refused, never trimmed"). Green for T020. `feat:`
- [X] T022 [UC2] Add `RetrievePayment_IdParameter_IsARequiredUuidInThePath` to
  `test/Integration/OpenApiDocumentTests.cs` (single parameter `id`, `in: path`, required,
  `format: uuid`). Red after T021: a plain `[ModelBinder]` makes the binding source "custom", so
  the document lists `id` as a **query** parameter. `test:`
- [X] T023 [UC2] Add `src/Http/Payments/PaymentIdFromRouteAttribute.cs`, a `ModelBinderAttribute`
  that selects `PaymentIdModelBinder` **and** sets `BindingSource = BindingSource.Path`; use
  `[PaymentIdFromRoute]` on the parameter. Green for T022. `feat:`
- [X] T024 Update `README.md` (Test strategy, API usage, Design Decisions: whitespace refused, not
  trimmed), `contracts/payments-api.yaml` (`id` description), `spec.md` (Edge Cases; close the
  "Surrounding whitespace" plan note), `research.md` R1/R11 and `data-model.md`. `docs:`

**Checkpoint**: `dotnet build -c Release` (0 warnings), `dotnet format --verify-no-changes` and
`dotnet test --filter "Category!=E2E"` all pass; `GET /api/payments/%20{id}%20` → `400`.

---

## Phase 9: Analysis follow-ups [UC2] (`/speckit-analyze` second run – G1, U1, I2, T2)

**Goal**: close the remaining coverage gaps. All are guard tests over behaviour that already
exists: each was green on first run and was shown to fail by temporarily breaking the behaviour
(same convention as UC1 T031/T058/T073).

- [X] T025 [UC2] (US1, U1) Add the hexadecimal `X` notation (`{0x…,…}`, URL-encoded) to
  `AcceptedNotations` in `RetrievePaymentEndpointTests`. Guard – shown red by temporarily refusing
  values starting with `{0x}` in `PaymentIdModelBinder`. `test:`
- [X] T026 [UC2] (US1/US3, G1 – FR-016, SC-006) Add
  `Get_WhenPaymentExists_LogsPaymentRetrievedUnderTheRequestTraceId` (a `GET` sent with a W3C
  `traceparent` header is logged as `PaymentRetrieved` under that trace id – a `200` body carries
  no `traceId`) and `Get_WhenIdIsInvalid_TraceIdCorrelatesWithThePaymentIdInvalidLogEntry`. Make
  both correlation tests (this one and T010's) also assert the `traceId` is a 32-hex id: without
  it, a request with no trace id passes as `null == null`. Guard – shown red by temporarily
  disabling the `ActivitySource` listener in `Program.cs`. `test:`
- [X] T027 [UC2] (US2, I2 – FR-008) Strengthen `Get_WhenIdIsUnknown_ResponseIsIdenticalForEveryId`
  to compare the whole body with `traceId` removed, not only `title`/`detail`/`status`. Guard –
  shown red by temporarily adding a random extension member to the `404` body. `test:`
- [X] T028 Update `spec.md` (FR-005/FR-006, Clarifications Q2, Key Entities, Assumptions: `X`
  form accepted, surrounding whitespace refused; Observations marked resolved; branch header),
  `plan.md` (branch header), the `RetrievePaymentAsync` XML `<remarks>` and `README.md` (the `X`
  form; `traceparent` correlation for `200` responses; test strategy). `docs:`
- [X] T029 (T2) Re-run the Definition of Done gates after Phases 8–9, including the E2E run
  against the simulator and coverage collection. **Verify**: all gates pass. `chore:`

---

## Dependencies & Execution Order

### Phase dependencies

| Phase | Depends on | Blocks |
|---|---|---|
| 1 Repository read | UC1 (`IPaymentRepository`, `InMemoryPaymentRepository`, `FakePaymentRepository`) | 2 |
| 2 RetrievePaymentService | 1 | 3 |
| 3 HTTP (US1 → US2 → US3) | 2 | 4, 5, 6 |
| 4 Card data guard | 3 | 6 |
| 5 API documentation | 3 (T014–T015 need only 3) | 6 |
| 6 E2E | 3–5 | 7 |
| 7 Definition of Done | 1–6 | – |
| 8 Whitespace refused (C1) | 3 (US3), 5 | 9 |
| 9 Analysis follow-ups | 3, 8 | – (T029 re-runs 7's gates) |

### Story order inside the HTTP phase

US1 (T007–T009) is the MVP; US2 (T010) extends `PaymentResultMapper`, US3 (T011–T012) extends
`InvalidModelStateResponder` and `PaymentResultMapper`; they can be done in either order after US1.

### Within each pair

Test task (Red) → the implementation task(s) that follow it (Green) → refactor → commit. Never
start the next test before the current pair is green. T007 has two Green steps (T008–T009).

## Parallel Opportunities

- Phase 1: T001 (test) can be written alongside Phase 2 planning, but T002/T003 must land before
  Phase 2 starts (the service depends on `GetByIdAsync`).
- Phase 5: T016 alongside T014–T015; T017 after T015 (needs the finished XML docs to summarize).

## Implementation Strategy

### MVP first

1. Phase 1 → repository read proven in isolation.
2. Phase 2 → use case proven with the fake repository.
3. Phase 3 US1 (T007–T009) → **STOP and validate** quickstart scenarios 1–5 against a running
   gateway (process a payment, then retrieve it).

### Incremental delivery

4. US2 (T010) → scenarios 6–7.
5. US3 (T011–T012) → scenarios 8–10.
6. Phase 4 → card-data guard.
7. Phase 5 → OpenAPI, `.http` and README.
8. Phase 6 → E2E journey.
9. Phase 7 → full Definition of Done (T019).

## Notes

- Summary: 29 task IDs, 28 done – Repository 3, Use case 3, HTTP 6 (US1 3, US2 1, US3 2), Card
  data guard 1, Documentation 3 (+ T014 dropped), E2E 1, Definition of Done 1, Whitespace refused 5
  (T020–T024), Analysis follow-ups 5 (T025–T029).
- No Setup or Foundational phase: UC2 adds no project, package, configuration key or entity
  (plan.md Summary); it reuses UC1's `Program.cs`, `PaymentGatewayFactory`,
  `WireMockBankFixture` and `FakePaymentRepository` unchanged except for the one added member
  (T003).
- Concurrency (FR-012) is not covered by a task: it is guaranteed by design (immutable `Payment` in
  a `ConcurrentDictionary`) and is checked in review, not by a non-deterministic stress test
  (research R7) – documented in README (T017), consistent with UC1's own concurrency note.
- Latency (SC-004) is observed manually via the quickstart, not load-tested (research R11; T019).
- T010's test may be green immediately after T009 lands; if so, record it in the commit message as
  a guard (same convention as UC1 T031/T058/T073) rather than skipping the task.

## Phase 10: Convergence

- [X] T030 Add the hexadecimal `{0x…}` form to the "Retrieval accepts every GUID notation" bullet in `README.md` Design Decisions & Assumptions (it lists canonical, no hyphens, braces and parentheses only) per FR-006 / T028 (partial)
- [X] T031 Add the hexadecimal `{0x…}` form to the notation list in the comment of the braces `GET` request in `src/PaymentGateway.Api/PaymentGateway.Api.http` per FR-017 / plan: `.http` "other notations" (partial)
