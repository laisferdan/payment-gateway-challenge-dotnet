# Research: Retrieve a Payment's Details (UC2)

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-09-26

UC2 builds on the design delivered by UC1 ([UC1 research](../001-process-payment/research.md),
R1–R17): one production project with `Domain/`, `Application/`, `Infrastructure/`, `Http/`;
`PaymentResultMapper` as the single result → HTTP translator; `AddProblemDetails()` with the
`traceId` customisation; JSON console logging with scopes; the in-memory repository. Only the
decisions UC2 adds or changes are recorded here. Format: Decision / Rationale / Alternatives.

Aligned with constitution **1.0.2**: UC1 now delivers the action-aware invalid-model factory
(`paymentStatus` only on `POST /api/payments`), `UseStatusCodePages()`, the framework log-level
rule and the card-number-in-path/body log test (UC1 research R3, R5, R15). UC2 relies on them.

## R1. Endpoint and identifier binding (FR-001, FR-005, FR-006)

- **Decision**: `GET /api/payments/{id}` on the existing `Http/PaymentsController`. The route
  template is **`{id}` without a `:guid` constraint**, and the action parameter is **`Guid id`**.
  ASP.NET Core model binding converts the route value with the platform's GUID parser, which
  accepts every GUID text form – canonical `D` (`8-4-4-4-12`), `N` (32 digits, no hyphens), `B`
  (`{…}`), `P` (`(…)`) and `X` (`{0x…,…}`) – case-insensitively, and ignores leading/trailing
  whitespace. Anything else fails binding and becomes the invalid-identifier outcome (R4).
- **Rationale**: Clarifications Q2/Q3 – any form the platform parses is the same identifier, and
  a GUID-typed parameter gives this validation for free. A `:guid` **route constraint** would make
  a malformed id an *unmatched route*, answered `404` – indistinguishable from "payment not found",
  which FR-005 forbids. Without the constraint the action is matched and binding reports the
  failure on `id`.
- **Surrounding whitespace** (spec "Notes for `/speckit-plan`"): **accepted**. An id such as
  `%203fa85f64-…%20` is parsed as the same GUID. This follows Q2 ("any form the platform
  parses"); whitespace carries no meaning in a GUID, so this interprets the same value rather than
  coercing an invalid one. Documented in the README's Design Decisions.
- **Alternatives**:
  - `{id:guid}` constraint (the template's choice) – rejected: malformed ids become `404`.
  - `string id` + `Guid.TryParseExact(id, "D")` in the controller – rejected by Q2 (canonical form
    only) and it hand-writes what binding already does.
  - A custom model binder that refuses whitespace – rejected: extra code to refuse a value that
    denotes the same GUID.

## R2. Found response (FR-002, FR-003, FR-015)

- **Decision**: `200 OK` with the **same response type UC1 returns, `PaymentResponse`**, built
  from the recorded `Payment` by the **same mapping** UC1 uses. `id` is serialized in canonical
  lowercase `D` form (System.Text.Json default), so the returned id always looks like the one in
  the processing response, whatever form the merchant used to ask. `cardNumberLastFour` is a JSON
  string; `status` is a string enum.
- **Rationale**: FR-003 ("exactly the same fields and values") is then true **by construction** –
  one type, one mapping; nothing can drift. The assessment's two response tables are identical.
- **Alternatives**:
  - A separate `GetPaymentResponse` with identical fields (the template has one) – rejected: two
    types and two mappings that must be kept identical by hand.
  - Keeping UC1's original name `PostPaymentResponse` – rejected: the type is now returned by
    `GET` too; UC1's artifacts were renamed to `PaymentResponse` before implementation.

## R3. Not found (FR-007, FR-008)

- **Decision**: `404 Not Found`, `ProblemDetails` produced by **`PaymentResultMapper`** from
  `RetrievePaymentResult.NotFound`: `type` `https://tools.ietf.org/html/rfc9110#section-15.5.5`,
  `title` "Payment not found", `detail` "No payment exists with the given id.", `status` 404,
  `traceId`. The body **does not echo the id** and is byte-for-byte the same for every unknown id
  apart from `traceId`. No `errorCode` (the status code alone identifies the outcome; UC1's
  `errorCode` distinguishes two bank failures that share nothing else).
- **Rationale**: Constitution API Design (`404` payment not found); FR-008 (same form for every
  unknown id, reveals nothing); one translator for every use-case result (UC1 R5).
- **Alternatives**: `204 No Content` or `200` with `null` (the template's behaviour – ambiguous
  for merchants); echoing the id in `detail` (harmless but makes responses differ per id).

## R4. Invalid identifier (FR-005; Clarifications Q1, Q4)

- **Decision**: `400 Bad Request` with a `ValidationProblemDetails`:
  `title` "Invalid payment id", `errors: { "id": ["The payment id must be a GUID, e.g. 3fa85f64-5717-4562-b3fc-2c963f66afa6."] }`,
  `traceId`, and **no `paymentStatus`** member.
  - **Mechanism**: binding failure → `[ApiController]` automatic `400` →
    `ApiBehaviorOptions.InvalidModelStateResponseFactory` → `PaymentResultMapper.ToUnreadableBodyResult`.
    UC1 already makes this method action-aware (UC1 R3, identified by
    `ControllerActionDescriptor.MethodInfo.Name`): `paymentStatus: "Rejected"` only for the
    processing action, a plain `ValidationProblemDetails` for any other. UC2 adds one branch: for
    the **retrieval** action the mapper uses the title "Invalid payment id" and the fixed `id`
    message below, and logs `PaymentIdInvalid` (R8).
  - **Message**: a fixed text that **never echoes the submitted value** – a merchant may paste
    anything into the path, including a card number (Principle VIII). The framework's default
    message (`The value '…' is not valid.`) is therefore not used.
  - The application service is never called, so the store is not searched (FR-005).
- **Rationale**: Q1 (a typo must be distinguishable from a missing payment), Q4 (`400`, no
  `paymentStatus` because no payment was attempted), one error family (`ProblemDetails` +
  `traceId`), one translator.
- **Alternatives**: validating in the action and calling `ValidationProblem()` (hand-written
  parsing, and still needs `[ApiController]`'s automatic 400 disabled for that action); keeping
  the factory unconditional (every invalid id would claim `paymentStatus: "Rejected"` – violates
  Q4 and Constitution 1.0.2); `SuppressModelStateInvalidFilter` globally (would change UC1).

## R5. Empty identifier and unmatched paths (spec Edge Cases, "Notes for `/speckit-plan`")

- **Decision**:
  - `GET /api/payments/%20` (whitespace only) reaches the action and fails binding → **`400`
    invalid id** (R4).
  - `GET /api/payments` and `GET /api/payments/` (empty id) match only the `POST` route →
    routing answers **`405 Method Not Allowed`**. It is never a payment, never a list and never an
    unexpected error.
  - Body-less routing errors (`405`, `404` unmatched path) are returned as `ProblemDetails` with
    `traceId` by `app.UseStatusCodePages()`, which **UC1 delivers** as a foundation (UC1 R5;
    Constitution 1.0.2, API Design). UC2 adds nothing for it; it only tests the empty-id case.
- **Rationale**: an empty path segment cannot reach the action, so it cannot be the invalid-id
  outcome; `405` is the correct HTTP answer for "retrieve" on the collection, which has no `GET`.
  UC1's `UseStatusCodePages` already keeps that `405` in the same error format with a trace id.
- **Alternatives**: a `GET /api/payments` action returning `400` for "missing id" (an endpoint
  that exists only to refuse requests – over-engineering); leaving `405`/`404` body-less
  (inconsistent error format, no `traceId`).

## R6. Application service and result (Constitution III, VI)

- **Decision**: `Application/RetrievePaymentService` – concrete, no interface (UC1 R11):
  `Retrieve(Guid id) → RetrievePaymentResult`, with `RetrievePaymentResult` = `Found(Payment)` |
  `NotFound`. Dependencies: `IPaymentRepository`, `ILogger<RetrievePaymentService>` only – it has
  **no `IAcquiringBank` dependency**, so it cannot contact the bank (FR-010). Synchronous: the
  only port it calls is an in-memory lookup with no I/O. The invalid-id outcome is not a member
  because a malformed id never reaches the service (R4).
- **Rationale**: one concrete application service per use case (Principle III); "not found" is an
  expected outcome and is modelled as a result, not an exception or `null` leaking into `Http/`
  (Principle VI); logging of the outcome lives in the use case, like UC1.
- **Alternatives**: the controller calling the repository directly (no use-case boundary; logging
  in the adapter); `Payment?` as the service result (a `null` that `Http/` must interpret);
  `async` methods (no asynchronous work exists – YAGNI).

## R7. Repository read and concurrency (FR-011, FR-012)

- **Decision**: `IPaymentRepository` gains **`GetById(Guid id) → Payment?`** (XML-documented);
  `InMemoryPaymentRepository` implements it with `ConcurrentDictionary.TryGetValue`. The key is
  the parsed `Guid`, so letter case and notation cannot affect the lookup (FR-006).
  **FR-012 is met by design**: `Payment` is immutable (UC1 data model) and is added to the
  dictionary only after it is fully built, and `ConcurrentDictionary` reads are atomic and
  lock-free – a reader sees the whole payment or nothing. It is **verified by review of these two
  properties, not by a stress test**: a multi-threaded test would be non-deterministic
  (Principle V) and would test framework code (UC1 R14).
- **Rationale**: the smallest port change that UC2's consumer needs (Interface Segregation);
  deterministic tests only.
- **Alternatives**: `TryGet(Guid, out Payment)` (awkward with nullable reference types and no
  benefit); a lock around reads (redundant); a concurrent load test (flaky, tests the BCL).

## R8. Observability (FR-016; Clarifications Q5)

- **Decision**: three `LoggerMessage` events, Information level, each carrying the request's
  `TraceId`/`SpanId` through the logging scope (UC1 R15):

  | EventId | Event | Fields | Written by |
  |---|---|---|---|
  | 3000 | `PaymentRetrieved` | `paymentId`, `status` | `RetrievePaymentService` |
  | 3001 | `PaymentNotFound` | `paymentId` (the parsed GUID) | `RetrievePaymentService` |
  | 3002 | `PaymentIdInvalid` | – (the raw value is **never** logged) | `Http/PaymentResultMapper.ToUnreadableBodyResult`, via its constructor-injected `ILogger<PaymentResultMapper>` |

  **No retrieval metric** (Q5); `PaymentGatewayMetrics` is unchanged. Logging a not-found id is
  safe (it is a parsed GUID and cannot carry card data) and is what enumeration monitoring – a
  production next step – would build on.
- **Framework logs** (Constitution 1.0.2, Principle VIII; delivered by UC1 R15): HTTP logging is
  not enabled and `Microsoft.AspNetCore` and `System.Net.Http.HttpClient` stay at `Warning`, so the
  host's "Request starting … /api/payments/{raw path}" line is never written – otherwise a card
  number pasted into the retrieval path would reach the logs.
- **Rationale**: every retrieval outcome is traceable by `traceId` (FR-016, SC-006) with built-in
  logging; the only untrusted value (a malformed id) never reaches a log or a response.
- **Alternatives**: a `paymentgateway.retrievals.outcomes` counter (declined in Q5); logging the
  malformed value truncated or hashed (still untrusted input; nothing to gain); logging
  invalid ids in the service (a malformed id never reaches it).

## R9. Card data protection (FR-014, FR-015)

- **Decision**: nothing new holds card data – `Payment` and `PaymentResponse` carry only the
  last four digits (text). The protections specific to UC2 are the fixed invalid-id message (R4),
  the unlogged raw id (R8) and the framework log levels delivered by UC1 (R8). The constitution's
  "card number in a request path appears in no log entry" test is **UC1's**
  (`Integration/CardDataLoggingTests.cs`, UC1 R15); UC2 adds the retrieval-specific case: a
  `GET` with an invalid id shaped like a card number (`4111111111111111`) – which reaches the
  retrieval action and its invalid-id log – asserts that neither the response body nor any
  collected log entry (`FakeLogger`, under the application's real logging configuration) contains
  it, plus the UC1-style assertion that no retrieval log or response contains the PAN or CVV of
  the processed payment.
- **Rationale**: Principle VIII – the absence of the PAN from logs is proven by a test.
- **Alternatives**: none needed.

## R10. API contract and documentation (FR-017; Clarifications Q6, Q7)

- **Decision**:
  - The design contract [`contracts/payments-api.yaml`](contracts/payments-api.yaml) describes
    **only the retrieval operation** and reuses UC1's schemas by relative `$ref` to
    `../../001-process-payment/contracts/payments-api.yaml` (`PaymentResponse`, `ProblemDetails`,
    and the `RoutingProblem` response for `405`/`404` produced by routing). It adds
    `InvalidPaymentIdProblemDetails`.
  - The **generated** OpenAPI document (Swashbuckle, UC1 R12) describes both operations: the GET
    action has XML docs (summary, `id` parameter with the accepted forms, remarks) and
    `[ProducesResponseType]` for `200` (`PaymentResponse`), `400`
    (`ValidationProblemDetails`), `404` (`ProblemDetails`) and `500` (`ProblemDetails`).
  - README (UC1 plan "README plan") updated: API usage (retrieval examples), Design Decisions
    (R1 accepted forms and whitespace, R2 one representation, R4 invalid id without
    `paymentStatus`, R5 `405` for an empty id, Q7 last four digits instead of a "masked card
    number", no ownership check, no retrieval metric, in-memory storage lost on restart),
    Observability (events 3000–3002), Test strategy (UC2 rows), Production next steps
    (merchant-scoped access, identifier-enumeration monitoring).
  - `.http` file gains: process-then-retrieve (named request, id taken from its response),
    retrieve in uppercase and braces, not found, invalid id.
- **Rationale**: Principle X – OpenAPI and README describe every endpoint, field, constraint and
  status code; UC1 FR-022 + UC2 FR-017 complete the documentation (Q6).
- **Alternatives**: copying UC1's whole contract into this folder (two copies to keep in sync).

## R11. Testing approach (Constitution IV, V; spec "Notes for `/speckit-plan`")

- **Decision**:

  | Level | File | Proves |
  |---|---|---|
  | Unit | `Unit/Application/RetrievePaymentServiceTests.cs` | `Found` returns the recorded payment unchanged; `NotFound` for an unknown id; `PaymentRetrieved` / `PaymentNotFound` logged with `paymentId` (`FakeLogger`); uses `FakePaymentRepository` (UC1 fake, gains `GetById`) |
  | Unit | `Unit/Infrastructure/InMemoryPaymentRepositoryTests.cs` | add then `GetById` returns the same payment; unknown id → `null` (the "add then retrieve" test deferred by UC1 R14) |
  | Integration | `Integration/RetrievePaymentEndpointTests.cs` | `POST` (WireMock authorized / declined) then `GET` → body **equal** to the `POST` body (FR-003, SC-001); leading-zero last four; `[Theory]` over accepted forms (upper-case `D`, `N`, `B`, `P`, `X`, surrounding whitespace) → same payment, canonical id returned; repeated `GET` identical (FR-009); WireMock receives **no** request during `GET` (FR-010); unknown and all-zeros id → `404` `ProblemDetails` with `traceId` equal to the logged `TraceId`; `[Theory]` over invalid ids (`abc`, `123`, 35 and 37 characters, a `g`, misplaced hyphen, unbalanced brace, whitespace only, `4111111111111111`) → `400`, `errors.id`, no `paymentStatus`, `traceId`, `PaymentIdInvalid` logged; `GET /api/payments/` → `405` `ProblemDetails`; no PAN/CVV/raw invalid id in any response or log |
  | Integration | `Integration/ProcessPaymentEndpointTests.cs` (UC1) | unchanged – already asserts an unbindable `POST` body returns `paymentStatus: "Rejected"` while other actions' `400`s do not (UC1 R3); it must stay green after the retrieval case is added to the mapper |
  | E2E | `EndToEnd/RetrievePaymentJourneyTests.cs` | `[Trait("Category","E2E")]`: process then retrieve against the real simulator for an Authorized (card ending 7) and a Declined (card ending 8) payment; retrieved details equal the processing response |

  **Performance (SC-004, < 500 ms for 95 %)**: not load-tested. Retrieval is an in-memory
  dictionary lookup with no I/O and no bank call; the quickstart shows how to observe the latency
  with `curl -w`. A load test would be over-engineering for this exercise (Principle VII).
- **Rationale**: each level has one purpose (UC1 R14); `[Theory]` for data variations
  (Principle V); the interaction "no bank call" is asserted at the HTTP boundary where it matters.
- **Alternatives**: a controller unit test (the controller only translates; integration covers
  it); a concurrency stress test (R7).

## R12. Dependencies

- **Decision**: **no new packages**. Everything used – routing, model binding,
  `ProblemDetails`, `LoggerMessage`, `ConcurrentDictionary` – is in .NET 8
  and ASP.NET Core; the test packages are UC1's (R13 there).
- **Rationale**: Principle VII.
