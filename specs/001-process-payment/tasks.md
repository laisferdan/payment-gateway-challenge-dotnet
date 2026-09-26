---

description: "Task list for UC1 – Process a Payment"
---

# Tasks: Process a Payment (UC1)

**Input**: Design documents from `specs/001-process-payment/` – [plan.md](plan.md),
[spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/payments-api.yaml](contracts/payments-api.yaml), [quickstart.md](quickstart.md)

**Prerequisites**: constitution 1.0.2 (`.specify/memory/constitution.md`)

**Tests**: mandatory (Constitution IV – TDD is non-negotiable). Every behaviour is a
**Red → Green pair**: a test task immediately followed by the implementation task it drives.
Setup and packaging tasks that cannot be test-first state how they are verified.

**Organization**: phases follow the delivery order requested for UC1 – foundation → domain rules
→ `ProcessPaymentService` → bank adapter → HTTP endpoint → observability → packaging →
documentation and E2E. All use-case work is labelled **`[UC1]`** (Constitution III uses use-case
IDs instead of `[US1]`); the spec's user story each task serves is given in brackets in the
description – **(US1)** Authorized/Declined, **(US2)** Rejected, **(US3)** bank failure. The HTTP
phase is split per user story, each ending with a checkpoint of what is demonstrable.

## Format: `[ID] [P?] [UC1?] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[UC1]**: use-case task (Setup, Foundational and packaging/docs tasks carry no label)
- Each task is one Red-Green-Refactor cycle and one Conventional Commit (`test:`, `feat:`,
  `refactor:`, `build:`, `ci:`, `docs:`); the commit type is given at the end of each task.

## Conventions for every test task

- Test names `<Unit>_<Scenario>_<ExpectedBehaviour>`; explicit `// Arrange`, `// Act`,
  `// Assert`; one Act; no conditionals or loops; boundaries and data variations as `[Theory]`
  (Constitution V).
- Unit tests use hand-written fakes only (`test/PaymentGateway.Api.Tests/Unit/Fakes/`) and
  `FakeTimeProvider` fixed at **2026-09-26T12:00:00Z**; no network, clock or randomness.
- Integration tests use `PaymentGatewayFactory` (T006) – the real pipeline and adapters, the bank
  replaced by WireMock.
- A test task is done when the test **fails for the right reason** (Red); the following
  implementation task is done when it passes and all other tests stay green (Green), followed by
  any refactoring.

Paths are relative to the repository root. `src/` = `src/PaymentGateway.Api/`,
`test/` = `test/PaymentGateway.Api.Tests/`.

---

## Phase 1: Setup (project reshaping)

**Purpose**: remove the template artefacts and make both projects satisfy the build gates.

- [ ] T001 Remove the template artefacts (research R11): delete `test/PaymentsControllerTests.cs`,
  `src/Controllers/`, `src/Enums/`, `src/Models/` and `src/Services/`; in `src/Program.cs` remove
  the `PaymentsRepository` registration, `UseHttpsRedirection()` and `UseAuthorization()`, and
  add `public partial class Program { }` at the end of the file. **Verify**: `dotnet build`
  succeeds. `refactor:`
- [ ] T002 [P] In `src/PaymentGateway.Api.csproj` set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`,
  `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and `<NoWarn>$(NoWarn);CS1591</NoWarn>`
  (keep `Nullable`, `ImplicitUsings`, Swashbuckle.AspNetCore 6.x). **Verify**:
  `dotnet build -c Release` → 0 warnings, 0 errors. `build:`
- [ ] T003 [P] In `test/PaymentGateway.Api.Tests.csproj` set `TreatWarningsAsErrors`; upgrade
  `Microsoft.AspNetCore.Mvc.Testing` to 8.0.x, `Microsoft.NET.Test.Sdk`, `xunit`,
  `xunit.runner.visualstudio` and `coverlet.collector` to current versions; add `WireMock.Net`,
  `Microsoft.Extensions.TimeProvider.Testing` and `Microsoft.Extensions.Diagnostics.Testing`
  (licences per research R13). Keep `Usings.cs` (`global using Xunit;`). **Verify**:
  `dotnet test` runs (0 tests) with 0 warnings. `build:`
- [ ] T004 [P] Replace `src/appsettings.json` with: `Logging:LogLevel` → `Default: Information`,
  `Microsoft.AspNetCore: Warning`, `System.Net.Http.HttpClient: Warning`; `AcquiringBank` →
  `BaseUrl: http://localhost:8080`, `TimeoutSeconds: 10`; `Swagger:Enabled: false`. Set
  `src/appsettings.Development.json` to `Swagger:Enabled: true` only (it MUST NOT lower any log
  level – Constitution VIII). **Verify**: by T010, T012 and T072. `build:`
- [ ] T005 Run `dotnet format --verify-no-changes` on the solution and fix any deviation in the
  touched files without editing `.editorconfig`. **Verify**: the command exits 0. `style:`

---

## Phase 2: Foundational (Program.cs skeleton)

**Purpose**: composition root, error format, health, logging and validated options that every
UC1 story needs.

**⚠️ CRITICAL**: no `[UC1]` task starts before this phase is complete.

- [ ] T006 Create the integration fixtures: `test/Integration/Fixtures/WireMockBankFixture.cs`
  (starts a `WireMockServer` on a free port, exposes its URL, resets stubs between tests) and
  `test/Integration/Fixtures/PaymentGatewayFactory.cs` (`WebApplicationFactory<Program>`;
  environment `Production` so no Development-only behaviour; sets `AcquiringBank:BaseUrl` to the
  WireMock URL and `AcquiringBank:TimeoutSeconds` to `1`; replaces `TimeProvider` with a
  `FakeTimeProvider` at 2026-09-26T12:00:00Z; `AddFakeLogging()` so collected logs honour the
  application's `appsettings.json` filters; exposes the `FakeLogCollector`). **Verify**:
  compiles; used from T007. `test:`
- [ ] T007 Write `test/Integration/HealthEndpointTests.cs`:
  `Health_WhenGatewayIsRunning_Returns200Healthy` (`GET /health` → `200`, body `Healthy`). Red.
  `test:`
- [ ] T008 In `src/Program.cs` add `AddHealthChecks()` and `MapHealthChecks("/health")` (liveness
  only – the bank is not checked, research R15). Green for T007. `feat:`
- [ ] T009 Write `test/Integration/ErrorFormatTests.cs`:
  `UnknownRoute_WhenRequested_ReturnsProblemDetailsWithTraceId` (`GET /api/unknown` → `404`,
  `Content-Type: application/problem+json`, `traceId` = 32 lowercase hex characters, equal to the
  `TraceId` scope value of the request's collected log entries). Red. `test:`
- [ ] T010 In `src/Program.cs` add `AddProblemDetails(o => o.CustomizeProblemDetails = …)` setting
  the `traceId` extension to `Activity.Current?.TraceId.ToString()` (research R15), then in the
  pipeline `UseExceptionHandler()` followed by `UseStatusCodePages()` (research R5, Constitution
  1.0.2 API Design); configure logging with `ClearProviders()` +
  `AddJsonConsole(o => o.IncludeScopes = true)`; do **not** register `AddHttpLogging` /
  `UseHttpLogging` or W3C logging (Constitution VIII). Green for T009. `feat:`
- [ ] T011 Write `test/Integration/StartupValidationTests.cs`:
  `Startup_WhenAcquiringBankOptionsAreInvalid_FailsFast` as a `[Theory]` over `BaseUrl` missing,
  empty, `not-a-url`, and `TimeoutSeconds` `0` and `61` → creating the host throws
  `OptionsValidationException`; plus `Startup_WhenOptionsAreValid_Starts` (`TimeoutSeconds` `1`
  and `60`). Red. `test:`
- [ ] T012 Create `src/Infrastructure/AcquiringBankOptions.cs` (section name constant
  `"AcquiringBank"`; `BaseUrl`: `[Required, Url]` absolute URI; `TimeoutSeconds`: `[Range(1, 60)]`,
  default `10`) and register it in `src/Program.cs` with
  `AddOptions<AcquiringBankOptions>().BindConfiguration(...).ValidateDataAnnotations().ValidateOnStart()`.
  Green for T011. `feat:`
- [ ] T013 In `src/Program.cs` register `TimeProvider.System` as a singleton, `AddControllers()`
  with `JsonStringEnumConverter` (camelCase is the default), and Swagger (`AddEndpointsApiExplorer`,
  `AddSwaggerGen` including the XML comment file) mapped **only when `Swagger:Enabled` is `true`**,
  never tied to the Development environment (Constitution XII). **Verify**: `dotnet run --project
  src/PaymentGateway.Api` → console lines are JSON; `http://localhost:5067/swagger` loads
  (Development sets the flag); asserted by T076. `feat:`

**Checkpoint (Foundation)**: `dotnet test` green (health, error format, startup validation);
`dotnet run` serves `/health` → `200 Healthy`; an unknown route returns a `ProblemDetails` with
`traceId`; a bad `AcquiringBank__BaseUrl` stops the gateway at startup.

---

## Phase 3: Domain validation rules [UC1]

**Goal**: every assessment rule lives in `src/Domain/`, unit-tested at its boundaries
(Constitution IX), with no HTTP or framework types.

**Independent test**: `dotnet test --filter "FullyQualifiedName~Unit.Domain"`.

- [ ] T014 [P] [UC1] Write `test/Unit/Domain/SupportedCurrenciesTests.cs` (US2): `[Theory]`
  `IsSupported` → true for `GBP`, `EUR`, `USD`; false for `gbp`, `Gbp`, `GB`, `GBPX`, `JPY`, `""`
  (ordinal, case-sensitive); `Length` is `3`. Red. `test:`
- [ ] T015 [P] [UC1] Create `src/Domain/SupportedCurrencies.cs`: codes `GBP`, `EUR`, `USD`,
  `Length = 3`, `IsSupported(string)` with ordinal comparison. Green for T014. `feat:`
- [ ] T016 [UC1] Write `test/Unit/Domain/PaymentRequestTests.cs` (US1):
  `Create_WhenAllFieldsAreValid_ReturnsRequest` (card `2222405343248877`, 12/2030, `GBP`, `1050`,
  `123`, today 2026-09-26) and `Create_WhenCardNumberHasLeadingZeroLastFour_KeepsZeros`
  (`…0012` → `CardNumberLastFour == "0012"`). Red. `test:`
- [ ] T017 [UC1] Create `src/Domain/ValidationError.cs` (`Field` in camelCase – `cardNumber`,
  `expiryMonth`, `expiryYear`, `currency`, `amount`, `cvv` – and `Message`, which "names the rule
  broken; never contains the submitted value") and `src/Domain/PaymentRequest.cs` with
  `Create(string? cardNumber, int? expiryMonth, int? expiryYear, string? currency, int? amount,
  string? cvv, DateOnly today)` returning a `CreatePaymentRequestResult` (either the
  `PaymentRequest` or a non-empty `IReadOnlyList<ValidationError>`); `CardNumberLastFour` = last
  `LastFourLength` (4) characters. Green for T016. `feat:`
- [ ] T018 [UC1] Add to `PaymentRequestTests` (US2) – card number (FR-003, "`MinCardNumberLength`–`MaxCardNumberLength`
  characters, ASCII digits `0-9` only"): `[Theory]` 13 digits ✗, 14 ✓, 19 ✓, 20 ✗, `null` ✗, `""` ✗,
  `"2222 4053 4324 8877"` ✗, `"2222-4053-4324-8877"` ✗, `"22224053432488a7"` ✗, Arabic-Indic
  digits `"٢٢٢٢٤٠٥٣٤٣٢٤٨٨٧٧"` ✗ → error on `cardNumber`, message not containing the value. Red. `test:`
- [ ] T019 [UC1] Implement the card-number rule in `src/Domain/PaymentRequest.cs` with constants
  `MinCardNumberLength = 14`, `MaxCardNumberLength = 19`, `LastFourLength = 4` and
  `char.IsAsciiDigit` (not `char.IsDigit`, which accepts non-ASCII digits). Green for T018. `feat:`
- [ ] T020 [UC1] Add to `PaymentRequestTests` (US2) – expiry month (FR-004, "`MinExpiryMonth`–`MaxExpiryMonth`"):
  `[Theory]` `0` ✗, `1` ✓, `12` ✓, `13` ✗, `null` ✗ → error on `expiryMonth`. Red. `test:`
- [ ] T021 [UC1] Implement the month rule with `MinExpiryMonth = 1`, `MaxExpiryMonth = 12`.
  Green for T020. `feat:`
- [ ] T022 [UC1] Add to `PaymentRequestTests` (US2) – expiry year and combination (FR-005, FR-006,
  "≥ current year and ≤ `MaxExpiryYear`", "`(ExpiryYear, ExpiryMonth) ≥ (today.Year, today.Month)`"),
  today 2026-09-26: `[Theory]` 2025 ✗, `27` ✗, 9999 ✓, 10000 ✗, `null` ✗ → error on `expiryYear`;
  `(9, 2026)` ✓ (current month), `(8, 2026)` ✗ (last month) → single error on `expiryYear`;
  `(13, 2020)` → errors on `expiryMonth` and `expiryYear` only (combination not evaluated when a
  part is invalid). Red. `test:`
- [ ] T023 [UC1] Implement the year rule (`MaxExpiryYear = 9999`) and the combination rule,
  evaluated only when month and year are individually valid; the combination error is reported on
  `expiryYear`. Green for T022. `feat:`
- [ ] T024 [UC1] Add to `PaymentRequestTests` (US2) – currency (FR-007, "exactly
  `SupportedCurrencies.Length` chars, uppercase, in `SupportedCurrencies`"): `[Theory]` `GBP`,
  `EUR`, `USD` ✓; `gbp`, `GB`, `JPY`, `null`, `""` ✗ → error on `currency` with message
  `Currency must be one of: GBP, EUR, USD.` Red. `test:`
- [ ] T025 [UC1] Implement the currency rule using `SupportedCurrencies`. Green for T024. `feat:`
- [ ] T026 [UC1] Add to `PaymentRequestTests` (US2) – amount (FR-008, "≥ `MinAmount`"):
  `[Theory]` `-1` ✗, `0` ✗, `1` ✓, `int.MaxValue` ✓, `null` ✗ → error on `amount`. Red. `test:`
- [ ] T027 [UC1] Implement the amount rule with `MinAmount = 1`. Green for T026. `feat:`
- [ ] T028 [UC1] Add to `PaymentRequestTests` (US2) – CVV (FR-009, "`MinCvvLength`–`MaxCvvLength`
  characters, ASCII digits only"): `[Theory]` `12` ✗, `123` ✓, `0123` ✓ (leading zero kept),
  `12345` ✗, `12a` ✗, `null` ✗, `""` ✗ → error on `cvv`, message not containing the value. Red. `test:`
- [ ] T029 [UC1] Implement the CVV rule with `MinCvvLength = 3`, `MaxCvvLength = 4`. Green for
  T028. `feat:`
- [ ] T030 [UC1] Add to `PaymentRequestTests` (US2) – aggregation and no coercion (FR-010, FR-011):
  `Create_WhenSeveralFieldsAreInvalid_ReturnsEveryError` (card `1234`, currency `gbp`, amount `0`
  → exactly three errors); `[Theory]` `Create_WhenValueNeedsTrimming_IsRejected` (cvv `" 123"`,
  card `"2222405343248877 "`, currency `" GBP"`). Red (or Green if already true – then record it
  as a guard and commit as `test:`). `test:`
- [ ] T031 [UC1] Make `Create` collect all errors instead of returning on the first one, and never
  trim/pad/case-convert. Green for T030. `refactor:`
- [ ] T032 [UC1] Add to `PaymentRequestTests` (US1): `ToString_Always_MasksCardNumberAndOmitsCvv`
  → contains `************8877`, expiry, currency and amount; does not contain
  `2222405343248877` nor the CVV (Constitution VIII). Red. `test:`
- [ ] T033 [UC1] Override `PaymentRequest.ToString()` accordingly. Green for T032. `feat:`
- [ ] T034 [P] [UC1] Write `test/Unit/Domain/PaymentTests.cs` (US1): `Create_FromValidRequest_CopiesSafeFields`
  (status, last four, expiry, currency, amount as submitted; `Id` not `Guid.Empty`);
  `Create_Twice_AssignsDifferentIds`; `ToString_Always_ExcludesCardNumberAndCvv`. Red. `test:`
- [ ] T035 [UC1] Create `src/Domain/PaymentStatus.cs` (`Authorized`, `Declined` – no `Rejected`)
  and `src/Domain/Payment.cs` (immutable; `Id` from `Guid.NewGuid()`; no card number, CVV or
  authorization code; `Create(PaymentRequest request, PaymentStatus status)`). Green for T034. `feat:`

**Checkpoint (Domain)**: every validation rule of the assessment is proven at its boundaries by
unit tests; `PaymentRequest` and `Payment` never print a card number or CVV.

---

## Phase 4: ProcessPaymentService [UC1]

**Goal**: the use case orchestrates validation, one bank call and recording, returning an explicit
result for each outcome (research R5, R7).

**Independent test**: `dotnet test --filter "FullyQualifiedName~Unit.Application"`.

- [ ] T036 [UC1] Create the application contracts (no behaviour; XML docs on the ports –
  Constitution X): `src/Application/IAcquiringBank.cs`
  (`Task<BankAuthorizationResult> RequestAuthorizationAsync(PaymentRequest, CancellationToken)`),
  `src/Application/BankAuthorizationResult.cs` (`Authorized` | `Declined` | `Failed(BankFailureKind)`),
  `src/Application/BankFailureKind.cs` (`Unavailable`, `Error`),
  `src/Application/IPaymentRepository.cs` (`void Add(Payment payment)` only),
  `src/Application/ProcessPaymentCommand.cs` (nullable raw values),
  `src/Application/ProcessPaymentResult.cs` (`Processed(Payment)` | `Rejected(IReadOnlyList<ValidationError>)`
  | `BankFailed(BankFailureKind)`). **Verify**: build; driven by T038+. `feat:`
- [ ] T037 [P] [UC1] Create hand-written fakes: `test/Unit/Fakes/FakeAcquiringBank.cs` (returns a
  configured `BankAuthorizationResult`; records call count and the last `PaymentRequest`) and
  `test/Unit/Fakes/FakePaymentRepository.cs` (records added payments). `test:`
- [ ] T038 [UC1] Write `test/Unit/Application/ProcessPaymentServiceTests.cs` (US1):
  `Process_WhenBankAuthorizes_ReturnsProcessedAuthorizedAndRecordsPayment` and
  `…WhenBankDeclines_ReturnsProcessedDeclinedAndRecordsPayment` → result `Processed`, payment fields
  as submitted, exactly one bank call with the validated request, exactly one recorded payment.
  Red. `test:`
- [ ] T039 [UC1] Create `src/Application/ProcessPaymentService.cs` (dependencies `IAcquiringBank`,
  `IPaymentRepository`, `TimeProvider`; today =
  `DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)`) implementing the valid path:
  validate → bank once → `Payment.Create` → `Add` → `Processed`. Green for T038. `feat:`
- [ ] T040 [UC1] Add (US2) `Process_WhenCommandIsInvalid_ReturnsRejectedWithoutCallingBank` →
  `Rejected` with all errors; bank call count `0`; nothing recorded (FR-012). Red. `test:`
- [ ] T041 [UC1] Implement the rejected branch in `ProcessPaymentService`. Green for T040. `feat:`
- [ ] T042 [UC1] Add (US3) `[Theory]` `Process_WhenBankFails_ReturnsBankFailedWithoutRecording`
  over `Unavailable` and `Error` → `BankFailed(kind)`; exactly one bank call; nothing recorded;
  no retry (FR-017, Constitution VIII). Red. `test:`
- [ ] T043 [UC1] Implement the bank-failure branch. Green for T042. `feat:`
- [ ] T044 [P] [UC1] Write `test/Unit/Application/ProcessPaymentCommandTests.cs` (US1):
  `ToString_Always_MasksCardNumberAndOmitsCvv`. Red. `test:`
- [ ] T045 [UC1] Override `ProcessPaymentCommand.ToString()`. Green for T044. `feat:`

**Checkpoint (Use case)**: all three outcomes – Processed, Rejected, BankFailed – are proven with
fakes: a rejected command never reaches the bank, a valid one reaches it exactly once, and bank
failures are never recorded.

---

## Phase 5: Acquiring bank adapter [UC1]

**Goal**: `AcquiringBankClient` speaks the simulator's snake_case contract and classifies every
answer (research R4, R6), tested against WireMock.

**Independent test**: `dotnet test --filter "FullyQualifiedName~AcquiringBankClientTests"`.

- [ ] T046 [UC1] Write `test/Integration/AcquiringBankClientTests.cs` (US1): bank `200`
  `{"authorized":true,"authorization_code":"0bb07405-…"}` → `Authorized`; `200`
  `{"authorized":false,"authorization_code":""}` → `Declined`; WireMock received exactly one
  `POST /payments` with body `card_number`, `expiry_date` `"04/2027"` (zero-padded `MM/yyyy`),
  `currency`, `amount`, `cvv`. Red. `test:`
- [ ] T047 [UC1] Create `src/Infrastructure/BankPaymentRequest.cs` and
  `src/Infrastructure/BankPaymentResponse.cs` (internal, `[JsonPropertyName]` snake_case) and
  `src/Infrastructure/AcquiringBankClient.cs` implementing `IAcquiringBank`; register it in
  `src/Program.cs` with `AddHttpClient<IAcquiringBank, AcquiringBankClient>` (`BaseAddress` and
  `Timeout` from `AcquiringBankOptions`, no retry handlers). Green for T046. `feat:`
- [ ] T048 [UC1] Add (US3) `[Theory]` cases: `503` → `Failed(Unavailable)`; response delayed beyond
  `TimeoutSeconds` (1 s) → `Unavailable`; connection refused (WireMock stopped) → `Unavailable`;
  `400`, `500` → `Failed(Error)`; body `not json`, body without `authorized`, `authorized: true`
  with empty `authorization_code` → `Error`; every case makes exactly **one** request. Plus
  `RequestAuthorization_WhenCallerCancels_PropagatesCancellation` (caller token cancelled →
  `OperationCanceledException`, not `Unavailable`). Red. `test:`
- [ ] T049 [UC1] Implement the classification in `AcquiringBankClient` (research R4/R6: shape check;
  timeout = `OperationCanceledException` while the caller's token is not cancelled;
  `HttpRequestException` → Unavailable; any other non-200 → Error). Green for T048. `feat:`
- [ ] T050 [P] [UC1] Write `test/Unit/Infrastructure/BankPaymentRequestTests.cs`:
  `ToString_Always_MasksCardNumberAndOmitsCvv`. Red. `test:`
- [ ] T051 [UC1] Override `BankPaymentRequest.ToString()`. Green for T050. `feat:`

**Checkpoint (Adapter)**: the adapter is proven for every simulator outcome plus timeout,
connection failure and unreadable answers, always with a single call.

---

## Phase 6: HTTP endpoint and mapping [UC1]

**Goal**: `POST /api/payments` translates HTTP ↔ use case through the single `PaymentResultMapper`
(research R1–R5).

**Independent test**: `dotnet test --filter "FullyQualifiedName~Integration"`.

### (US1) Merchant receives the bank's decision

- [ ] T052 [UC1] Write `test/Integration/ProcessPaymentEndpointTests.cs` (US1): WireMock authorizes
  → `200`, JSON `id` (GUID), `status: "Authorized"`, `cardNumberLastFour: "8877"` (string),
  `expiryMonth`, `expiryYear`, `currency`, `amount` as submitted, no `cardNumber`/`cvv` members, no
  `Location` header, body text contains neither the PAN nor the CVV; WireMock declines →
  `status: "Declined"`; card `…0012` → `"0012"`; two requests → two different ids. Red. `test:`
- [ ] T053 [UC1] Create `src/Http/PostPaymentRequest.cs` (nullable members mirroring
  `ProcessPaymentCommand`), `src/Http/PaymentResponse.cs` (`id`, `status`, `cardNumberLastFour`,
  `expiryMonth`, `expiryYear`, `currency`, `amount`; built by a single `From(Payment)` mapping),
  `src/Http/PaymentResultMapper.cs` (`Processed` → `200` + `PaymentResponse`),
  `src/Http/PaymentsController.cs` (`[ApiController]`, `[Route("api/payments")]`,
  `[HttpPost] ProcessPaymentAsync` – HTTP translation only) and
  `src/Infrastructure/InMemoryPaymentRepository.cs` (`ConcurrentDictionary<Guid, Payment>`);
  register repository (singleton), service and mapper in `src/Program.cs`. Green for T052. `feat:`
- [ ] T054 [P] [UC1] Write `test/Unit/Http/PostPaymentRequestTests.cs`:
  `ToString_Always_MasksCardNumberAndOmitsCvv`. Red. `test:`
- [ ] T055 [UC1] Override `PostPaymentRequest.ToString()`. Green for T054. `feat:`
- [ ] T056 [UC1] Add to `test/Integration/ErrorFormatTests.cs`: `[Theory]`
  `WrongMethod_OnPaymentsRoute_ReturnsProblemDetailsWithTraceId` (`GET` and `PUT /api/payments` →
  `405` `application/problem+json` with `traceId`, no `paymentStatus`); and
  `UnexpectedError_WhenRepositoryThrows_Returns500WithoutDetails` (factory replaces
  `IPaymentRepository` with a throwing stub → `500` `ProblemDetails` with `traceId`, no exception
  message or stack trace). Red if either fails. `test:`
- [ ] T057 [UC1] Make T056 green: `UseExceptionHandler()`/`UseStatusCodePages()` placed before
  `MapControllers()`/`MapHealthChecks()`, and no developer exception page outside Development. If
  T056 already passes (behaviour delivered by T010), record it in the commit message as a guard
  and make no code change. `fix:`

**Checkpoint (US1 – MVP)**: with `docker compose up -d bank_simulator` and `dotnet run`, quickstart
scenarios 1–3 work: a card ending in 7 returns `200 Authorized`, ending in 8 `200 Declined`, and
last four digits keep their leading zeros – never the PAN or CVV.

### (US2) Merchant is told why a request was rejected

- [ ] T058 [UC1] Add to `ProcessPaymentEndpointTests` (US2): card `1234`, currency `gbp`, amount `0`
  → `400` `application/problem+json`, `title: "Payment rejected"`, `paymentStatus: "Rejected"`,
  `errors` with keys `cardNumber`, `currency`, `amount`, `traceId`; WireMock received **no**
  request; no error message contains `1234`. Red. `test:`
- [ ] T059 [UC1] Implement `Rejected` → `400` `ValidationProblemDetails` (`type`
  `https://tools.ietf.org/html/rfc9110#section-15.5.1`, `title`, `errors`, `paymentStatus`) in
  `PaymentResultMapper`. Green for T058. `feat:`
- [ ] T060 [UC1] Add (US2) `[Theory]` `Post_WhenBodyIsUnreadable_ReturnsRejected`: `"amount":"ten"`,
  `"cardNumber":2222405343248877` (number), `"amount":2147483648`, invalid JSON `{`, empty body →
  `400` with `paymentStatus: "Rejected"`, `errors` keyed by field (`$.amount` → `amount`; root
  errors → `body`), fixed messages that never echo the submitted value, `traceId`; WireMock
  received no request. Red. `test:`
- [ ] T061 [UC1] Configure `ApiBehaviorOptions.InvalidModelStateResponseFactory` in
  `src/Program.cs` to call `PaymentResultMapper`: when
  `context.ActionDescriptor is ControllerActionDescriptor d &&
  d.MethodInfo.Name == nameof(PaymentsController.ProcessPaymentAsync)` → the Rejected body **with**
  `paymentStatus: "Rejected"`; any other action → plain `ValidationProblemDetails` with `traceId`
  and **no** `paymentStatus` (research R3, Constitution 1.0.2). Use `MethodInfo.Name`, not
  `ActionName` (which drops `Async`). Green for T060. `feat:`

**Checkpoint (US2)**: quickstart scenarios 4–6 work: invalid and unreadable requests return `400`
Rejected listing every invalid field, the bank is never called, and no message echoes card data.

### (US3) Merchant is told when the bank cannot process the payment

- [ ] T062 [UC1] Add to `ProcessPaymentEndpointTests` (US3) `[Theory]`: WireMock `503` and a delay
  beyond the timeout → `503`, `errorCode: "bank_unavailable"`, `title: "Payment could not be
  processed"`; WireMock `400` and body `not json` → `502`, `errorCode: "bank_error"`; all
  `application/problem+json` with `traceId`, **no** `paymentStatus`, no PAN/CVV; WireMock received
  exactly one request. Red. `test:`
- [ ] T063 [UC1] Implement `BankFailed` → `503`/`502` `ProblemDetails` (`type` rfc9110 15.6.4 /
  15.6.3, `detail`, `errorCode` extension) in `PaymentResultMapper`. Green for T062. `feat:`

**Checkpoint (US3)**: quickstart scenarios 7–8 work: a card ending in 0, or a stopped simulator,
returns `503 bank_unavailable` – never `Declined` – and nothing is recorded; `502 bank_error` is
proven by the integration tests.

---

## Phase 7: Observability assertions [UC1]

**Goal**: one log entry per outcome and per bank call, the two instruments of meter
`PaymentGateway`, `traceId` correlation, and no card data in any log (research R15, Constitution
VIII, XI).

- [ ] T064 [UC1] Add to `test/Unit/Application/ProcessPaymentServiceTests.cs` (`FakeLogger`):
  (US1) `PaymentProcessed` EventId 1000, Information, `paymentId`, `status`, `currency`, `amount`;
  (US2) `PaymentRejected` 1001, Information, `invalidFields` = field **names** only;
  (US3) `PaymentBankFailed` 1002, Warning, `failureKind`, `currency`, `amount`; no entry contains
  the PAN or CVV. Red. `test:`
- [ ] T065 [UC1] Add `LoggerMessage` source-generated methods (partial class
  `src/Application/ProcessPaymentService.Log.cs`) and call them from each branch. Green for T064.
  `feat:`
- [ ] T066 [UC1] Add to `ProcessPaymentServiceTests` (`MetricCollector<long>` with a real
  `IMeterFactory` from `new ServiceCollection().AddMetrics()`): each outcome records exactly one
  `paymentgateway.payments.outcomes` measurement with `result` = `authorized` | `declined` |
  `rejected` | `bank_unavailable` | `bank_error`. Red. `test:`
- [ ] T067 [UC1] Create `src/Application/PaymentGatewayMetrics.cs` (meter `PaymentGateway`;
  `Counter<long>` `paymentgateway.payments.outcomes`, unit `{payment}`; `Histogram<double>`
  `paymentgateway.bank.request.duration`, unit `s`), register it as a singleton, inject it into
  `ProcessPaymentService` and record the outcome. Green for T066. `feat:`
- [ ] T068 [UC1] Add to `AcquiringBankClientTests`: `BankCallCompleted` 2000 (`durationMs`,
  `outcome`) for authorized/declined; `BankCallFailed` 2001 Warning (`durationMs`, `failureKind`,
  `httpStatusCode` – null on timeout); one `paymentgateway.bank.request.duration` measurement per
  call tagged `outcome`. Red. `test:`
- [ ] T069 [UC1] Add the adapter's `LoggerMessage` methods and the duration measurement using
  `TimeProvider.GetTimestamp()`/`GetElapsedTime()` in `src/Infrastructure/AcquiringBankClient.cs`.
  Green for T068. `feat:`
- [ ] T070 [UC1] Add to `ProcessPaymentEndpointTests` (US2): an unreadable body logs
  `PaymentRequestUnreadable` EventId 1003 **once**, Information, `invalidFields` = binding paths
  (e.g. `$.amount`), no body content, carrying the request `TraceId` (equal to the response
  `traceId`); **no** `paymentgateway.payments.outcomes` measurement is recorded. Red. `test:`
- [ ] T071 [UC1] Log `PaymentRequestUnreadable` from `PaymentResultMapper` when building the
  processing Rejected response for an unbindable payload (logger resolved from
  `HttpContext.RequestServices`); do not touch `PaymentGatewayMetrics`. Green for T070. `feat:`
- [ ] T072 [UC1] Write `test/Integration/CardDataLoggingTests.cs` (guard, Constitution VIII 1.0.2),
  with the application's real logging configuration: card `4111111111111111` / CVV `123` sent
  (a) in a request path `GET /api/payments/4111111111111111`, (b) in a valid `POST` body
  (authorized, rejected with another invalid field, bank unavailable), (c) in an unreadable `POST`
  body → the card number and CVV appear in **no** collected log entry (message or structured
  state). Expected green on first run; prove it can fail by temporarily setting
  `Microsoft.AspNetCore` to `Information` in the test configuration (red), then revert. **Verify**:
  both runs observed. `test:`

**Checkpoint (Observability)**: `dotnet run` + quickstart §6 – `dotnet-counters monitor -n
PaymentGateway.Api --counters PaymentGateway` shows outcomes and bank durations; every error's
`traceId` is found in the JSON logs; no card number appears in any log line.

---

## Phase 8: Packaging, hosting and CI

**Purpose**: one `docker compose up` runs gateway + simulator; CI enforces every gate
(research R16, R17, Constitution XII).

- [ ] T073 [P] Create `Dockerfile` (multi-stage `mcr.microsoft.com/dotnet/sdk:8.0` → publish
  `src/PaymentGateway.Api` → `mcr.microsoft.com/dotnet/aspnet:8.0`; install `curl` for the
  healthcheck; `USER $APP_UID`; `EXPOSE 8080`; `ENTRYPOINT ["dotnet","PaymentGateway.Api.dll"]`)
  and `.dockerignore` (`bin/`, `obj/`, `test/`, `specs/`, `.git/`). **Verify**:
  `docker build -t payment-gateway .` succeeds; `docker run --rm payment-gateway id` shows a
  non-root uid. `build:`
- [ ] T074 Add the `payment_gateway` service to `docker-compose.yml` exactly as research R16
  (build `.`, ports `8090:8080`, `AcquiringBank__BaseUrl: http://bank_simulator:8080`,
  `Swagger__Enabled: "true"`, `depends_on: bank_simulator`, curl healthcheck on `/health`), leaving
  `bank_simulator`, `version` and `imposters/` unchanged. **Verify**: `docker compose up --build` →
  `curl http://localhost:8090/health` returns `200 Healthy`; `docker compose ps` shows
  `payment_gateway` healthy; a `POST` with a card ending in 7 to port 8090 returns `Authorized`.
  `build:`
- [ ] T075 [P] Create `.github/workflows/ci.yml` (push and pull_request; `ubuntu-latest`; checkout,
  setup-dotnet 8.0.x, restore, `build --no-restore -c Release`, `dotnet format
  --verify-no-changes --no-restore`, `dotnet test --no-build -c Release --filter "Category!=E2E"
  --collect:"XPlat Code Coverage" --results-directory ./coverage`, upload the Cobertura file with
  `actions/upload-artifact@v4`, `docker build -t payment-gateway .`). **Verify**: the same commands
  pass locally; the workflow is green on the first push. `ci:`

---

## Phase 9: Documentation and end-to-end journeys

- [ ] T076 [P] Write `test/Integration/OpenApiDocumentTests.cs`: with `Swagger:Enabled=true`,
  `GET /swagger/v1/swagger.json` → `POST /api/payments` documents `200`, `400`, `500`, `502`,
  `503` and the `PaymentResponse` schema with XML summaries; with the default (`false`) →
  `/swagger/v1/swagger.json` returns `404`. Red. `test:`
- [ ] T077 Add XML documentation comments to `PaymentsController`, `PostPaymentRequest`,
  `PaymentResponse` (and confirm them on `IAcquiringBank`, `IPaymentRepository`), and
  `[ProducesResponseType]` for `200` (`PaymentResponse`), `400` (`ValidationProblemDetails`), `502`,
  `503`, `500` (`ProblemDetails`) on `ProcessPaymentAsync` (FR-022). Green for T076. `docs:`
- [ ] T078 [UC1] Write `test/EndToEnd/ProcessPaymentJourneyTests.cs` with
  `[Trait("Category", "E2E")]`: against the real simulator (`AcquiringBank:BaseUrl`
  `http://localhost:8080`), card ending `7` → `200 Authorized`, ending `8` → `200 Declined`, ending
  `0` → `503 bank_unavailable`. **Verify**: `docker compose up -d bank_simulator` then
  `dotnet test --filter "Category=E2E"` passes, and `dotnet test --filter "Category!=E2E"` does not
  run it. `test:`
- [ ] T079 [P] Create `src/PaymentGateway.Api.http` with `@base = http://localhost:5067` (comment:
  `http://localhost:8090` for compose) and requests for Authorized (card ending 7), Declined
  (ending 8), Rejected (card `1234`, currency `gbp`, amount `0`), Bank unavailable (ending 0),
  unknown route, and `GET {{base}}/health`. **Verify**: each request returns the status in
  quickstart §5. `docs:`
- [ ] T080 [P] Rewrite `README.md` with the ten sections of the plan's "README plan" (overview; run
  locally; run with Docker; test commands per level and coverage; test strategy with the risks
  covered – card data leakage incl. framework logs, bank failure misreported as Declined,
  validation boundaries; Mermaid architecture and flow diagrams; API usage with 200/400/502/503
  examples; observability – events 1000–1003/2000–2001, `traceId`, the two instruments and the
  scope of `rejected`, `http.server.request.duration` for unreadable bodies, framework log levels
  and why, `/health`; design decisions and assumptions; production next steps) plus "How this was
  built" linking `specs/` and the constitution. **Verify**: every command in it runs as written.
  `docs:`
- [ ] T081 Run the Definition of Done: `dotnet build -c Release` (0 warnings),
  `dotnet format --verify-no-changes`, `dotnet test --filter "Category!=E2E" --collect:"XPlat Code
  Coverage"`, the E2E run, and every scenario of [quickstart.md](quickstart.md) §1–§8; fix any
  gap in the task that introduced it. **Verify**: all gates pass. `chore:`

---

## Dependencies & Execution Order

### Phase dependencies

| Phase | Depends on | Blocks |
|---|---|---|
| 1 Setup | – | everything |
| 2 Foundational | 1 | 3–9 |
| 3 Domain | 2 | 4, 6 |
| 4 ProcessPaymentService | 3 | 6, 7 |
| 5 Bank adapter | 2 (options), 4 (`IAcquiringBank`, `BankAuthorizationResult`) | 6, 7 |
| 6 HTTP (US1 → US2 → US3) | 4, 5 | 7, 8, 9 |
| 7 Observability | 6 | 9 |
| 8 Packaging | 6 (T074 verifies a POST; T075 builds the image from T073) | 9 |
| 9 Docs and E2E | 6–8 (T076–T077 need only 6) | – |

### Story order inside the HTTP phase

US1 (T052–T057) is the MVP; US2 (T058–T061) and US3 (T062–T063) each extend
`PaymentResultMapper` and can be done in either order after US1.

### Within each pair

Test task (Red) → the implementation task that follows it (Green) → refactor → commit. Never start
the next test before the current pair is green.

## Parallel Opportunities

- Phase 1: T002, T003, T004 (different project files).
- Phase 3: T014/T015 (`SupportedCurrencies`) alongside T016–T033 (`PaymentRequest`); T034 once
  T017 exists.
- Phase 4: T037 (fakes) and T044 alongside T036.
- Phase 5: T050 alongside T046–T049.
- Phase 6: T054 alongside T052–T053.
- Phase 8: T073 and T075 in parallel; T074 after T073.
- Phase 9: T076 → T077; T079 and T080 in parallel with T078.

### Parallel example (Phase 3)

```text
Developer A: T014 → T015  (SupportedCurrencies)
Developer B: T016 → T017 → T018 → … → T033  (PaymentRequest rules)
then:        T034 → T035  (Payment)
```

## Implementation Strategy

### MVP first

1. Phases 1–2 → foundation checkpoint.
2. Phases 3–5 → domain, use case and adapter proven in isolation.
3. Phase 6 US1 (T052–T057) → **STOP and validate** quickstart scenarios 1–3 against the simulator.

### Incremental delivery

4. US2 (T058–T061) → scenarios 4–6.
5. US3 (T062–T063) → scenarios 7–8.
6. Phase 7 → observability gates of the Definition of Done.
7. Phases 8–9 → Docker, CI, docs, E2E → full Definition of Done (T081).

## Notes

- Summary: 81 tasks – Setup 5, Foundational 8, Domain 22, Use case 10, Adapter 6, HTTP 12
  (US1 6, US2 4, US3 2), Observability 9, Packaging 3, Docs/E2E 6.
- Tasks naming a guard test (T030, T057, T072) may be green on first run because an earlier task
  already delivered the behaviour; the task says how the test is shown to fail when the behaviour
  is absent.
- UC2 (`specs/002-retrieve-payment`) builds on this list: `IPaymentRepository.GetById`, the `GET`
  action, its mapper cases and tests.
