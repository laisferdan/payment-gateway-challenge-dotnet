---

description: "Task list for UC1 – Process a Payment"
---

# Tasks: Process a Payment (UC1)

**Input**: Design documents from `specs/001-process-payment/` – [plan.md](plan.md),
[spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md),
[contracts/payments-api.yaml](contracts/payments-api.yaml), [quickstart.md](quickstart.md)

**Prerequisites**: constitution 1.0.2 (`.specify/memory/constitution.md`)

**Tests**: mandatory (Constitution IV – TDD is non-negotiable). Every behaviour is a
**Red → Green pair**: a test task immediately followed by the implementation task(s) it drives.
A test that does not compile because the type it needs does not exist yet counts as **Red**; the
type is created in the Green step. Setup, scaffolding and packaging tasks that cannot be
test-first state how they are verified.

**Organization**: phases follow the delivery order requested for UC1 – foundation → domain rules
→ `ProcessPaymentService` → bank adapter → HTTP endpoint → observability → packaging →
documentation and E2E. All use-case work is labelled **`[UC1]`** (Constitution III uses use-case
IDs instead of `[US1]`); the spec's user story each task serves is given in brackets in the
description – **(US1)** Authorized/Declined, **(US2)** Rejected, **(US3)** bank failure. The HTTP
phase is split per user story, each ending with a checkpoint of what is demonstrable.

**Revision** (2026-09-26): `/speckit-analyze` findings D1–D4, C1–C6, E1, E2, F1 and F2 applied;
E3 and E4 accepted (see Notes); tasks renumbered – 84 tasks.

## Format: `[ID] [P?] [UC1?] Description`

- **[P]**: can run in parallel (different files, no dependency on an incomplete task)
- **[UC1]**: use-case task (Setup, Foundational and packaging/docs tasks carry no label)
- Each task is one Red-Green-Refactor step and one Conventional Commit (`test:`, `feat:`,
  `refactor:`, `build:`, `ci:`, `docs:`); the commit type is given at the end of each task.

## Conventions for every test task

- Test names `<Unit>_<Scenario>_<ExpectedBehaviour>`; explicit `// Arrange`, `// Act`,
  `// Assert`; one Act; no conditionals or loops; boundaries and data variations as `[Theory]`
  (Constitution V).
- Unit tests use hand-written fakes only (`test/Unit/Fakes/`) and `FakeTimeProvider` fixed at
  **2026-09-26T12:00:00Z**; no network, clock or randomness.
- Integration tests use `PaymentGatewayFactory` (T005) – the real pipeline and adapters, the bank
  replaced by WireMock. Components under test (e.g. `IAcquiringBank`) are **resolved from the
  factory's `Services`**, so the real `HttpClient` setup and options are used.
- A test task is done when the test **fails for the right reason** (Red); the implementation
  task(s) that follow are done when it passes and all other tests stay green (Green), followed by
  any refactoring.

Paths are relative to the repository root. `src/` = `src/PaymentGateway.Api/`,
`test/` = `test/PaymentGateway.Api.Tests/`.

---

## Phase 1: Setup (project reshaping)

**Purpose**: remove the template artefacts and make both projects satisfy the build gates.

- [X] T001 Remove the template artefacts (research R11): delete `test/PaymentsControllerTests.cs`,
  `src/Controllers/`, `src/Enums/`, `src/Models/` and `src/Services/`; in `src/Program.cs` remove
  the `PaymentsRepository` registration, `UseHttpsRedirection()` and `UseAuthorization()`, and add
  `public partial class Program { }` at the end of the file. The template's `AddControllers()` and
  Development-only Swagger stay until the tasks that test them (T053, T078). **Verify**:
  `dotnet build` succeeds. `refactor:`
- [X] T002 [P] In `src/PaymentGateway.Api.csproj` set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`,
  `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and `<NoWarn>$(NoWarn);CS1591</NoWarn>`
  (keep `Nullable`, `ImplicitUsings`, Swashbuckle.AspNetCore 6.x). **Verify**:
  `dotnet build -c Release` → 0 warnings, 0 errors. `build:`
- [X] T003 [P] In `test/PaymentGateway.Api.Tests.csproj` set `TreatWarningsAsErrors`; upgrade
  `Microsoft.AspNetCore.Mvc.Testing` to 8.0.x, `Microsoft.NET.Test.Sdk`, `xunit`,
  `xunit.runner.visualstudio` and `coverlet.collector` to current versions; add `WireMock.Net`,
  `Microsoft.Extensions.TimeProvider.Testing` and `Microsoft.Extensions.Diagnostics.Testing`
  (licences per research R13). Keep `Usings.cs` (`global using Xunit;`). **Verify**:
  `dotnet test` runs (0 tests) with 0 warnings. `build:`
- [X] T004 Run `dotnet format --verify-no-changes` on the solution and fix any deviation in the
  touched files without editing `.editorconfig`. **Verify**: the command exits 0. `style:`

---

## Phase 2: Foundational (Program.cs skeleton)

**Purpose**: composition root, error format, health, framework-log guard and validated options
that every UC1 story needs.

**⚠️ CRITICAL**: no `[UC1]` task starts before this phase is complete.

- [X] T005 Create the integration fixtures: `test/Integration/Fixtures/WireMockBankFixture.cs`
  (starts a `WireMockServer` on a free port, exposes its URL, resets stubs between tests) and
  `test/Integration/Fixtures/PaymentGatewayFactory.cs` (`WebApplicationFactory<Program>`;
  environment `Production` so no Development-only behaviour; sets `AcquiringBank:BaseUrl` to the
  WireMock URL and `AcquiringBank:TimeoutSeconds` to `1`; accepts per-test configuration
  overrides; replaces `TimeProvider` with a `FakeTimeProvider` at 2026-09-26T12:00:00Z;
  `AddFakeLogging()` so collected logs honour the application's `appsettings.json` filters;
  exposes `Services` and the `FakeLogCollector`). **Verify**: compiles; used from T006. `test:`
- [X] T006 Write `test/Integration/HealthEndpointTests.cs`:
  `Health_WhenGatewayIsRunning_Returns200Healthy` (`GET /health` → `200`, body `Healthy`). Red.
  `test:`
- [X] T007 In `src/Program.cs` add `AddHealthChecks()` and `MapHealthChecks("/health")` (liveness
  only – the bank is not checked, research R15). Green for T006. `feat:`
- [X] T008 Write `test/Integration/ErrorFormatTests.cs`:
  `UnknownRoute_WhenRequested_ReturnsProblemDetailsWithTraceId` (`GET /api/unknown` → `404`,
  `Content-Type: application/problem+json`, `type`/`title`/`status` present, `traceId` matching
  `^[0-9a-f]{32}$`). Shape and format only: an unknown route writes no application log entry, so
  the log ↔ response correlation is asserted where the application logs (T065, T071). Red. `test:`
- [X] T009 In `src/Program.cs` add `AddProblemDetails(o => o.CustomizeProblemDetails = …)` setting
  the `traceId` extension to `Activity.Current?.TraceId.ToString()` (research R15), then in the
  pipeline `UseExceptionHandler()` followed by `UseStatusCodePages()` (research R5, Constitution
  1.0.2 API Design); configure logging with `ClearProviders()` +
  `AddJsonConsole(o => o.IncludeScopes = true)`; do **not** register `AddHttpLogging` /
  `UseHttpLogging` or W3C logging (Constitution VIII). Green for T008. `feat:`
- [X] T010 Write `test/Integration/CardDataLoggingTests.cs` – request-path part (Constitution VIII
  1.0.2), with the application's real logging configuration:
  `RequestPath_WhenItContainsACardNumber_IsNotLogged` (`GET /api/payments/4111111111111111` → the
  card number appears in **no** collected entry, message or structured state) and `[Theory]`
  `Logging_ForFrameworkRequestCategories_IsWarningOrAbove` over
  `Microsoft.AspNetCore.Hosting.Diagnostics`, `Microsoft.AspNetCore.Routing` and
  `System.Net.Http.HttpClient.IAcquiringBank.LogicalHandler` →
  `ILoggerFactory.CreateLogger(category).IsEnabled(LogLevel.Information)` is `false` (logger
  factory from the factory's `Services`). Red: the `System.Net.Http.HttpClient` category is not
  restricted yet. To show the path test itself can fail, temporarily set `Microsoft.AspNetCore` to
  `Information` – the host's "Request starting" entry then logs the path. `test:`
- [X] T011 Set `Logging:LogLevel` in `src/appsettings.json` to `Default: Information`,
  `Microsoft.AspNetCore: Warning` and `System.Net.Http.HttpClient: Warning` (Constitution VIII
  1.0.2); make sure `src/appsettings.Development.json` sets no lower level for these categories.
  Green for T010. `feat:`
- [X] T012 Write `test/Integration/StartupValidationTests.cs`:
  `Startup_WhenAcquiringBankOptionsAreInvalid_FailsFast` as a `[Theory]` over `BaseUrl` `""` (stands
  for "missing": `appsettings.json` always supplies a value, so the override is an empty string),
  `not-a-url`, and `TimeoutSeconds` `0` and `61` → creating the host throws
  `OptionsValidationException`; plus `Startup_WhenOptionsAreValid_Starts` (`TimeoutSeconds` `1`
  and `60`). Red. `test:`
- [X] T013 Create `src/Infrastructure/AcquiringBankOptions.cs` (section name constant
  `"AcquiringBank"`; `BaseUrl`: `[Required, Url]` absolute URI; `TimeoutSeconds`: `[Range(1, 60)]`,
  default `10`), add `"AcquiringBank": { "BaseUrl": "http://localhost:8080", "TimeoutSeconds": 10 }`
  to `src/appsettings.json`, and register the options in `src/Program.cs` with
  `AddOptions<AcquiringBankOptions>().BindConfiguration(...).ValidateDataAnnotations().ValidateOnStart()`.
  Green for T012. `feat:`
- [X] T014 **Scaffolding** – register `TimeProvider.System` as a singleton in `src/Program.cs`. It
  has no behaviour of its own: it is consumed by `ProcessPaymentService` (T038, registered in
  T053) and `AcquiringBankClient` (T070), and replaced by `FakeTimeProvider` in every test.
  **Verify**: `dotnet build`; `dotnet test` stays green. `build:`

**Checkpoint (Foundation)**: `dotnet test` green (health, error format, framework-log guard,
startup validation); `dotnet run` serves `/health` → `200 Healthy` and writes JSON log lines; an
unknown route returns a `ProblemDetails` with `traceId`; a bad `AcquiringBank__BaseUrl` stops the
gateway at startup.

---

## Phase 3: Domain validation rules [UC1]

**Goal**: every assessment rule lives in `src/Domain/`, unit-tested at its boundaries
(Constitution IX), with no HTTP or framework types.

**Independent test**: `dotnet test --filter "FullyQualifiedName~Unit.Domain"`.

- [X] T015 [P] [UC1] Write `test/Unit/Domain/SupportedCurrenciesTests.cs` (US2): `[Theory]`
  `IsSupported` → true for `GBP`, `EUR`, `USD`; false for `gbp`, `Gbp`, `GB`, `GBPX`, `JPY`, `""`
  (ordinal, case-sensitive); `Length` is `3`. Red. `test:`
- [X] T016 [P] [UC1] Create `src/Domain/SupportedCurrencies.cs`: codes `GBP`, `EUR`, `USD`,
  `Length = 3`, `IsSupported(string)` with ordinal comparison. Green for T015. `feat:`
- [X] T017 [UC1] Write `test/Unit/Domain/PaymentRequestTests.cs` (US1):
  `Create_WhenAllFieldsAreValid_ReturnsRequest` (card `2222405343248877`, 12/2030, `GBP`, `1050`,
  `123`, today 2026-09-26) and `Create_WhenCardNumberHasLeadingZeroLastFour_KeepsZeros`
  (`…0012` → `CardNumberLastFour == "0012"`). Red. `test:`
- [X] T018 [UC1] Create `src/Domain/ValidationError.cs` (`Field` in camelCase – `cardNumber`,
  `expiryMonth`, `expiryYear`, `currency`, `amount`, `cvv` – and `Message`, which "names the rule
  broken; never contains the submitted value"), `src/Domain/CreatePaymentRequestResult.cs`
  (either the `PaymentRequest` or a non-empty `IReadOnlyList<ValidationError>`) and
  `src/Domain/PaymentRequest.cs` with `Create(string? cardNumber, int? expiryMonth,
  int? expiryYear, string? currency, int? amount, string? cvv, DateOnly today)` returning a
  `CreatePaymentRequestResult`; `CardNumberLastFour` = last `LastFourLength` (4) characters. Green
  for T017. `feat:`
- [X] T019 [UC1] Add to `PaymentRequestTests` (US2) – card number (FR-003, "`MinCardNumberLength`–`MaxCardNumberLength`
  characters, ASCII digits `0-9` only"): `[Theory]` 13 digits ✗, 14 ✓, 19 ✓, 20 ✗, `null` ✗, `""` ✗,
  `"2222 4053 4324 8877"` ✗, `"2222-4053-4324-8877"` ✗, `"22224053432488a7"` ✗, Arabic-Indic
  digits `"٢٢٢٢٤٠٥٣٤٣٢٤٨٨٧٧"` ✗ → error on `cardNumber`, message not containing the value. Red. `test:`
- [X] T020 [UC1] Implement the card-number rule in `src/Domain/PaymentRequest.cs` with constants
  `MinCardNumberLength = 14`, `MaxCardNumberLength = 19`, `LastFourLength = 4` and
  `char.IsAsciiDigit` (not `char.IsDigit`, which accepts non-ASCII digits). Green for T019. `feat:`
- [X] T021 [UC1] Add to `PaymentRequestTests` (US2) – expiry month (FR-004, "`MinExpiryMonth`–`MaxExpiryMonth`"):
  `[Theory]` `0` ✗, `1` ✓, `12` ✓, `13` ✗, `null` ✗ → error on `expiryMonth`. Red. `test:`
- [X] T022 [UC1] Implement the month rule with `MinExpiryMonth = 1`, `MaxExpiryMonth = 12`.
  Green for T021. `feat:`
- [X] T023 [UC1] Add to `PaymentRequestTests` (US2) – expiry year and combination (FR-005, FR-006,
  "≥ current year and ≤ `MaxExpiryYear`", "`(ExpiryYear, ExpiryMonth) ≥ (today.Year, today.Month)`"),
  today 2026-09-26: `[Theory]` 2025 ✗, `27` ✗, 9999 ✓, 10000 ✗, `null` ✗ → error on `expiryYear`;
  `(9, 2026)` ✓ (current month), `(8, 2026)` ✗ (last month) → single error on `expiryYear`;
  `(13, 2020)` → errors on `expiryMonth` and `expiryYear` only (combination not evaluated when a
  part is invalid). Red. `test:`
- [X] T024 [UC1] Implement the year rule (`MaxExpiryYear = 9999`) and the combination rule,
  evaluated only when month and year are individually valid; the combination error is reported on
  `expiryYear`. Green for T023. `feat:`
- [X] T025 [UC1] Add to `PaymentRequestTests` (US2) – currency (FR-007, "exactly
  `SupportedCurrencies.Length` chars, uppercase, in `SupportedCurrencies`"): `[Theory]` `GBP`,
  `EUR`, `USD` ✓; `gbp`, `GB`, `JPY`, `null`, `""` ✗ → error on `currency` with message
  `Currency must be one of: GBP, EUR, USD.` Red. `test:`
- [X] T026 [UC1] Implement the currency rule using `SupportedCurrencies`. Green for T025. `feat:`
- [X] T027 [UC1] Add to `PaymentRequestTests` (US2) – amount (FR-008, "≥ `MinAmount`"):
  `[Theory]` `-1` ✗, `0` ✗, `1` ✓, `int.MaxValue` ✓, `null` ✗ → error on `amount`. Red. `test:`
- [X] T028 [UC1] Implement the amount rule with `MinAmount = 1`. Green for T027. `feat:`
- [X] T029 [UC1] Add to `PaymentRequestTests` (US2) – CVV (FR-009, "`MinCvvLength`–`MaxCvvLength`
  characters, ASCII digits only"): `[Theory]` `12` ✗, `123` ✓, `0123` ✓ (leading zero kept),
  `12345` ✗, `12a` ✗, `null` ✗, `""` ✗ → error on `cvv`, message not containing the value. Red. `test:`
- [X] T030 [UC1] Implement the CVV rule with `MinCvvLength = 3`, `MaxCvvLength = 4`. Green for
  T029. `feat:`
- [X] T031 [UC1] Add to `PaymentRequestTests` (US2) – aggregation and no coercion (FR-010, FR-011):
  `Create_WhenSeveralFieldsAreInvalid_ReturnsEveryError` (card `1234`, currency `gbp`, amount `0`
  → exactly three errors); `[Theory]` `Create_WhenValueNeedsTrimming_IsRejected` (cvv `" 123"`,
  card `"2222405343248877 "`, currency `" GBP"`). Red (or Green if already true – then record it
  as a guard and commit as `test:`). `test:`
- [X] T032 [UC1] Make `Create` collect all errors instead of returning on the first one, and never
  trim/pad/case-convert. Green for T031. `refactor:`
- [X] T033 [UC1] Add to `PaymentRequestTests` (US1): `ToString_Always_MasksCardNumberAndOmitsCvv`
  → contains `************8877`, expiry, currency and amount; does not contain
  `2222405343248877` nor the CVV (Constitution VIII). Red. `test:`
- [X] T034 [UC1] Override `PaymentRequest.ToString()` accordingly. Green for T033. `feat:`
- [X] T035 [P] [UC1] Write `test/Unit/Domain/PaymentTests.cs` (US1): `Create_FromValidRequest_CopiesSafeFields`
  (status, last four, expiry, currency, amount as submitted; `Id` not `Guid.Empty`);
  `Create_Twice_AssignsDifferentIds`; `ToString_Always_ExcludesCardNumberAndCvv`. Red. `test:`
- [X] T036 [UC1] Create `src/Domain/PaymentStatus.cs` (`Authorized`, `Declined` – no `Rejected`)
  and `src/Domain/Payment.cs` (immutable; `Id` from `Guid.NewGuid()`; no card number, CVV or
  authorization code; `Create(PaymentRequest request, PaymentStatus status)`). Green for T035. `feat:`

**Checkpoint (Domain)**: every validation rule of the assessment is proven at its boundaries by
unit tests; `PaymentRequest` and `Payment` never print a card number or CVV.

---

## Phase 4: ProcessPaymentService [UC1]

**Goal**: the use case orchestrates validation, one bank call and recording, returning an explicit
result for each outcome (research R5, R7). Each application type is created in the Green step of
the first test that needs it.

**Independent test**: `dotnet test --filter "FullyQualifiedName~Unit.Application"`.

- [X] T037 [UC1] Write `test/Unit/Application/ProcessPaymentServiceTests.cs` (US1) together with the
  hand-written fakes `test/Unit/Fakes/FakeAcquiringBank.cs` (returns a configured
  `BankAuthorizationResult`; records call count and the last `PaymentRequest`) and
  `test/Unit/Fakes/FakePaymentRepository.cs` (records added payments):
  `Process_WhenBankAuthorizes_ReturnsProcessedAuthorizedAndRecordsPayment` and
  `…WhenBankDeclines_ReturnsProcessedDeclinedAndRecordsPayment` → result `Processed`, payment fields
  as submitted, exactly one bank call with the validated request, exactly one recorded payment.
  Red (does not compile: the application types do not exist yet). `test:`
- [X] T038 [UC1] Create, with XML docs on the ports (Constitution X):
  `src/Application/IAcquiringBank.cs`
  (`Task<BankAuthorizationResult> RequestAuthorizationAsync(PaymentRequest)`),
  `src/Application/BankAuthorizationResult.cs` (`Authorized` | `Declined`),
  `src/Application/IPaymentRepository.cs` (`void Add(Payment payment)` only),
  `src/Application/ProcessPaymentCommand.cs` (nullable raw values),
  `src/Application/ProcessPaymentResult.cs` (`Processed(Payment)`) and
  `src/Application/ProcessPaymentService.cs` (dependencies `IAcquiringBank`, `IPaymentRepository`,
  `TimeProvider`; today = `DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)`): validate →
  bank once → `Payment.Create` → `Add` → `Processed`. Green for T037. `feat:`
- [X] T039 [UC1] Add (US2) `Process_WhenCommandIsInvalid_ReturnsRejectedWithoutCallingBank` →
  `Rejected` with all errors; bank call count `0`; nothing recorded (FR-012). Red (does not
  compile). `test:`
- [X] T040 [UC1] Add `ProcessPaymentResult.Rejected(IReadOnlyList<ValidationError>)` and the
  rejected branch in `ProcessPaymentService`. Green for T039. `feat:`
- [X] T041 [UC1] Add (US3) `[Theory]` `Process_WhenBankFails_ReturnsBankFailedWithoutRecording`
  over `Unavailable` and `Error` → `BankFailed(kind)`; exactly one bank call; nothing recorded;
  no retry (FR-017, Constitution VIII). Red (does not compile). `test:`
- [X] T042 [UC1] Create `src/Application/BankFailureKind.cs` (`Unavailable`, `Error`), add
  `BankAuthorizationResult.Failed(BankFailureKind)` and `ProcessPaymentResult.BankFailed(BankFailureKind)`,
  and implement the bank-failure branch. Green for T041. `feat:`
- [X] T043 [P] [UC1] Write `test/Unit/Application/ProcessPaymentCommandTests.cs` (US1):
  `ToString_Always_MasksCardNumberAndOmitsCvv`. Red. `test:`
- [X] T044 [UC1] Override `ProcessPaymentCommand.ToString()`. Green for T043. `feat:`

**Checkpoint (Use case)**: all three outcomes – Processed, Rejected, BankFailed – are proven with
fakes: a rejected command never reaches the bank, a valid one reaches it exactly once, and bank
failures are never recorded.

---

## Phase 5: Acquiring bank adapter [UC1]

**Goal**: `AcquiringBankClient` speaks the simulator's snake_case contract and classifies every
answer (research R4, R6), tested against WireMock through the real DI setup.

**Independent test**: `dotnet test --filter "FullyQualifiedName~AcquiringBankClientTests"`.

- [X] T045 [UC1] Write `test/Integration/AcquiringBankClientTests.cs` (US1), resolving
  `IAcquiringBank` from `PaymentGatewayFactory.Services` (real `HttpClient` registration and
  options): bank `200` `{"authorized":true,"authorization_code":"0bb07405-…"}` → `Authorized`;
  `200` `{"authorized":false,"authorization_code":""}` → `Declined`; WireMock received exactly one
  `POST /payments` with body `card_number`, `expiry_date` `"04/2027"` (zero-padded `MM/yyyy`),
  `currency`, `amount`, `cvv`. Red. `test:`
- [X] T046 [UC1] Create `src/Infrastructure/BankPaymentRequest.cs` and
  `src/Infrastructure/BankPaymentResponse.cs` (internal, `[JsonPropertyName]` snake_case) and
  `src/Infrastructure/AcquiringBankClient.cs` implementing `IAcquiringBank`; register it in
  `src/Program.cs` with `AddHttpClient<IAcquiringBank, AcquiringBankClient>` (`BaseAddress` and
  `Timeout` from `AcquiringBankOptions`, no retry handlers). Green for T045. `feat:`
- [X] T047 [UC1] Add (US3) `[Theory]` cases, client resolved from the factory: `503` →
  `Failed(Unavailable)`; response delayed beyond `TimeoutSeconds` (1 s) → `Unavailable`;
  connection refused → `Unavailable`, with the factory's `AcquiringBank:BaseUrl` pointed at an
  **unused local port** (`http://127.0.0.1:<port>`, the port obtained by binding a `TcpListener`
  to port 0 and releasing it) – the shared WireMock server is never stopped; `400`, `500` →
  `Failed(Error)`; body `not json`, body without `authorized`, `authorized: true` with empty
  `authorization_code` → `Error`; every WireMock case makes exactly **one** request. Red. `test:`
- [X] T048 [UC1] Implement the classification in `AcquiringBankClient` (research R4/R6: shape check;
  timeout = `OperationCanceledException` from `HttpClient.Timeout`;
  `HttpRequestException` → Unavailable; any other non-200 → Error). Green for T047. `feat:`
- [X] T049 [P] [UC1] Write `test/Unit/Infrastructure/BankPaymentRequestTests.cs`:
  `ToString_Always_MasksCardNumberAndOmitsCvv`. Red. `test:`
- [X] T050 [UC1] Override `BankPaymentRequest.ToString()`. Green for T049. `feat:`

**Checkpoint (Adapter)**: the adapter is proven for every simulator outcome plus timeout,
connection failure and unreadable answers, always with a single call, through the real DI setup.

---

## Phase 6: HTTP endpoint and mapping [UC1]

**Goal**: `POST /api/payments` translates HTTP ↔ use case through the single `PaymentResultMapper`
(research R1–R5); error bodies are typed so the OpenAPI document matches the contract
(research R2, R4).

**Independent test**: `dotnet test --filter "FullyQualifiedName~Integration"`.

### (US1) Merchant receives the bank's decision

- [X] T051 [UC1] Write `test/Integration/ProcessPaymentEndpointTests.cs` (US1): WireMock authorizes
  → `200`, JSON `id` (GUID), `status: "Authorized"` (a **string** – this drives the enum setting
  in T053), `cardNumberLastFour: "8877"` (string), `expiryMonth`, `expiryYear`, `currency`,
  `amount` as submitted, no `cardNumber`/`cvv` members, no `Location` header, body text contains
  neither the PAN nor the CVV; WireMock declines → `status: "Declined"`; card `…0012` → `"0012"`;
  two requests → two different ids; a valid body with an unknown extra member `"foo": 1` → `200`
  (spec Edge Cases). Red. `test:`
- [X] T052 [UC1] Green step 1/3 for T051 (analysis "T053a"): create `src/Http/PaymentResponse.cs`
  (`id`, `status`, `cardNumberLastFour`, `expiryMonth`, `expiryYear`, `currency`, `amount`; a single
  `From(Payment)` mapping) and `src/Http/PaymentResultMapper.cs` with the success case
  (`Processed` → `200` + `PaymentResponse`). `feat:`
- [X] T053 [UC1] Green step 2/3 for T051 (analysis "T053b"): create `src/Http/PostPaymentRequest.cs`
  (nullable members mirroring `ProcessPaymentCommand`) and `src/Http/PaymentsController.cs`
  (`[ApiController]`, `[Route("api/payments")]`, `[HttpPost] ProcessPaymentAsync` – HTTP
  translation only); in `src/Program.cs` add `JsonStringEnumConverter` to the existing
  `AddControllers()` JSON options (camelCase is the default) – T051's `status: "Authorized"` fails
  without it – keep `MapControllers()`, and register `ProcessPaymentService` and
  `PaymentResultMapper`. `feat:`
- [X] T054 [UC1] Green step 3/3 for T051 (analysis "T053c"): create
  `src/Infrastructure/InMemoryPaymentRepository.cs` (`ConcurrentDictionary<Guid, Payment>`,
  implements `IPaymentRepository`) and register it as a singleton. T051 is green after this step.
  `feat:`
- [X] T055 [P] [UC1] Write `test/Unit/Http/PostPaymentRequestTests.cs`:
  `ToString_Always_MasksCardNumberAndOmitsCvv`. Red. `test:`
- [X] T056 [UC1] Override `PostPaymentRequest.ToString()`. Green for T055. `feat:`
- [X] T057 [UC1] Add to `test/Integration/ErrorFormatTests.cs`: `[Theory]`
  `WrongMethod_OnPaymentsRoute_ReturnsProblemDetailsWithTraceId` (`GET` and `PUT /api/payments` →
  `405` `application/problem+json` with `traceId`, no `paymentStatus`); and
  `UnexpectedError_WhenRepositoryThrows_Returns500WithoutDetails` (factory replaces
  `IPaymentRepository` with a throwing stub → `500` `ProblemDetails` with `traceId`, no exception
  message or stack trace). Red if either fails. `test:`
- [X] T058 [UC1] Make T057 green: `UseExceptionHandler()`/`UseStatusCodePages()` placed before
  `MapControllers()`/`MapHealthChecks()`, and no developer exception page outside Development. If
  T057 already passes (behaviour delivered by T009), record it in the commit message as a guard
  and make no code change. `fix:`

**Checkpoint (US1 – MVP)**: with `docker compose up -d bank_simulator` and `dotnet run`, quickstart
scenarios 1–3 work: a card ending in 7 returns `200 Authorized`, ending in 8 `200 Declined`, and
last four digits keep their leading zeros – never the PAN or CVV.

### (US2) Merchant is told why a request was rejected

- [X] T059 [UC1] Add to `ProcessPaymentEndpointTests` (US2): card `1234`, currency `gbp`, amount `0`
  → `400` `application/problem+json`, `type` `https://tools.ietf.org/html/rfc9110#section-15.5.1`,
  `title: "Payment rejected"`, `paymentStatus: "Rejected"`, `errors` with keys `cardNumber`,
  `currency`, `amount`, `traceId`; WireMock received **no** request; no error message contains
  `1234`. Red. `test:`
- [X] T060 [UC1] Create `src/Http/PaymentRejectedProblemDetails.cs` (`: ValidationProblemDetails`;
  `PaymentStatus` = `"Rejected"`, serialized as `paymentStatus`) and map `Rejected` → `400` +
  `PaymentRejectedProblemDetails` in `PaymentResultMapper`. Green for T059. `feat:`
- [X] T061 [UC1] Add (US2) `[Theory]` `Post_WhenBodyIsUnreadable_ReturnsRejected`: `"amount":"ten"`,
  `"cardNumber":2222405343248877` (number), `"amount":2147483648`, invalid JSON `{`, empty body →
  `400` `PaymentRejectedProblemDetails` with `paymentStatus: "Rejected"`, `errors` keyed by field
  (`$.amount` → `amount`; root errors → `body`), fixed messages that never echo the submitted
  value, `traceId`; WireMock received no request. Red. `test:`
- [X] T062 [UC1] Configure `ApiBehaviorOptions.InvalidModelStateResponseFactory` in
  `src/Program.cs` to call `PaymentResultMapper`: when
  `context.ActionDescriptor is ControllerActionDescriptor d &&
  d.MethodInfo.Name == nameof(PaymentsController.ProcessPaymentAsync)` →
  `PaymentRejectedProblemDetails` (**with** `paymentStatus`); any other action → plain
  `ValidationProblemDetails` with `traceId` and **no** `paymentStatus` (research R3, Constitution
  1.0.2). Use `MethodInfo.Name`, not `ActionName` (which drops `Async`). Green for T061. `feat:`

**Checkpoint (US2)**: quickstart scenarios 4–6 work: invalid and unreadable requests return `400`
Rejected listing every invalid field, the bank is never called, and no message echoes card data.

### (US3) Merchant is told when the bank cannot process the payment

- [X] T063 [UC1] Add to `ProcessPaymentEndpointTests` (US3) `[Theory]`: WireMock `503` and a delay
  beyond the timeout → `503`, `errorCode: "bank_unavailable"`, `title: "Payment could not be
  processed"`; WireMock `400` and body `not json` → `502`, `errorCode: "bank_error"`; all
  `application/problem+json` with `traceId`, **no** `paymentStatus`, no PAN/CVV; WireMock received
  exactly one request. Red. `test:`
- [X] T064 [UC1] Create `src/Http/BankFailureProblemDetails.cs` (`: ProblemDetails`; `ErrorCode`
  serialized as `errorCode` – `bank_unavailable` | `bank_error`) and map `BankFailed` →
  `503`/`502` + `BankFailureProblemDetails` (`type` rfc9110 15.6.4 / 15.6.3, `detail`) in
  `PaymentResultMapper`. Green for T063. `feat:`

**Checkpoint (US3)**: quickstart scenarios 7–8 work: a card ending in 0, or a stopped simulator,
returns `503 bank_unavailable` – never `Declined` – and nothing is recorded; `502 bank_error` is
proven by the integration tests.

---

## Phase 7: Observability assertions [UC1]

**Goal**: one log entry per outcome and per bank call, the two instruments of meter
`PaymentGateway`, `traceId` correlation, and no card data in any log (research R15, Constitution
VIII, XI).

- [X] T065 [UC1] Add to `test/Unit/Application/ProcessPaymentServiceTests.cs` (`FakeLogger`):
  (US1) `PaymentProcessed` EventId 1000, Information, `paymentId`, `status`, `currency`, `amount`;
  (US2) `PaymentRejected` 1001, Information, `invalidFields` = field **names** only;
  (US3) `PaymentBankFailed` 1002, Warning, `failureKind`, `currency`, `amount`; no entry contains
  the PAN or CVV. Add to `test/Integration/ProcessPaymentEndpointTests.cs` the **correlation**
  assertions (moved from T008): for a Rejected (US2) and a Bank unavailable (US3) response, the
  body's `traceId` equals the `TraceId` scope value of the `PaymentRejected` / `PaymentBankFailed`
  entry. Red. `test:`
- [X] T066 [UC1] Add `LoggerMessage` source-generated methods in the partial class
  `src/Application/ProcessPaymentService.Log.cs` and call them from each branch. Green for T065.
  `feat:`
- [X] T067 [UC1] Add to `ProcessPaymentServiceTests` (`MetricCollector<long>` with a real
  `IMeterFactory` from `new ServiceCollection().AddMetrics()`): each outcome records exactly one
  `paymentgateway.payments.outcomes` measurement with `result` = `authorized` | `declined` |
  `rejected` | `bank_unavailable` | `bank_error`. Red. `test:`
- [X] T068 [UC1] Create `src/Application/PaymentGatewayMetrics.cs` (meter `PaymentGateway`;
  `Counter<long>` `paymentgateway.payments.outcomes`, unit `{payment}`; `Histogram<double>`
  `paymentgateway.bank.request.duration`, unit `s`), register it as a singleton, inject it into
  `ProcessPaymentService` and record the outcome. Green for T067. `feat:`
- [X] T069 [UC1] Add to `AcquiringBankClientTests` (client resolved from the factory):
  `BankCallCompleted` 2000 (`durationMs`, `outcome`) for authorized/declined; `BankCallFailed` 2001
  Warning (`durationMs`, `failureKind`, `httpStatusCode` – null on timeout); one
  `paymentgateway.bank.request.duration` measurement per call tagged `outcome`. Red. `test:`
- [X] T070 [UC1] Add the adapter's `LoggerMessage` methods and the duration measurement using
  `TimeProvider.GetTimestamp()`/`GetElapsedTime()` in `src/Infrastructure/AcquiringBankClient.cs`.
  Green for T069. `feat:`
- [X] T071 [UC1] Add to `ProcessPaymentEndpointTests` (US2): an unreadable body logs
  `PaymentRequestUnreadable` EventId 1003 **once**, Information, `invalidFields` = binding paths
  (e.g. `$.amount`), no body content, and its `TraceId` scope value equals the response `traceId`
  (correlation, moved from T008); **no** `paymentgateway.payments.outcomes` measurement is
  recorded. Red. `test:`
- [X] T072 [UC1] Log `PaymentRequestUnreadable` from `PaymentResultMapper` when building the
  processing Rejected response for an unbindable payload (logger resolved from
  `HttpContext.RequestServices`); do not touch `PaymentGatewayMetrics`. Green for T071. `feat:`
- [X] T073 [UC1] Add to `test/Integration/CardDataLoggingTests.cs` the **body** cases (guard,
  Constitution VIII 1.0.2; the request-path case is T010): card `4111111111111111` / CVV `123` in
  a valid `POST` body (authorized, rejected with another invalid field, bank unavailable) and in
  an unreadable `POST` body → neither appears in any collected log entry (message or structured
  state). Expected green on first run; prove it can fail by temporarily adding a log call that
  writes the command's card number in `ProcessPaymentService` (red), then revert. **Verify**:
  both runs observed. `test:`

**Checkpoint (Observability)**: `dotnet run` + quickstart §6 – `dotnet-counters monitor -n
PaymentGateway.Api --counters PaymentGateway` shows outcomes and bank durations; every error's
`traceId` is found in the JSON logs; no card number appears in any log line.

---

## Phase 8: Packaging, hosting and CI

**Purpose**: one `docker compose up` runs gateway + simulator; CI enforces every gate
(research R16, R17, Constitution XII).

- [ ] T074 [P] Create `Dockerfile` (multi-stage `mcr.microsoft.com/dotnet/sdk:8.0` → publish
  `src/PaymentGateway.Api` → `mcr.microsoft.com/dotnet/aspnet:8.0`; install `curl` for the
  healthcheck; `USER $APP_UID`; `EXPOSE 8080`; `ENTRYPOINT ["dotnet","PaymentGateway.Api.dll"]`)
  and `.dockerignore` (`bin/`, `obj/`, `test/`, `specs/`, `.git/`). **Verify**:
  `docker build -t payment-gateway .` succeeds; `docker run --rm payment-gateway id` shows a
  non-root uid. `build:`
- [ ] T075 Add the `payment_gateway` service to `docker-compose.yml` exactly as research R16
  (build `.`, ports `8090:8080`, `AcquiringBank__BaseUrl: http://bank_simulator:8080`,
  `Swagger__Enabled: "true"`, `depends_on: bank_simulator`, curl healthcheck on `/health`), leaving
  `bank_simulator`, `version` and `imposters/` unchanged. **Verify**: `docker compose up --build` →
  `curl http://localhost:8090/health` returns `200 Healthy`; `docker compose ps` shows
  `payment_gateway` healthy; a `POST` with a card ending in 7 to port 8090 returns `Authorized`.
  `build:`
- [X] T076 [P] Create `.github/workflows/ci.yml` (push and pull_request; `ubuntu-latest`; checkout,
  setup-dotnet 8.0.x, restore, `build --no-restore -c Release`, `dotnet format
  --verify-no-changes --no-restore`, `dotnet test --no-build -c Release --filter "Category!=E2E"
  --collect:"XPlat Code Coverage" --results-directory ./coverage`, upload the Cobertura file with
  `actions/upload-artifact@v4`, `docker build -t payment-gateway .`). **Verify**: the same commands
  pass locally; the workflow is green on the first push. `ci:`

---

## Phase 9: Documentation and end-to-end journeys

- [X] T077 Write `test/Integration/OpenApiDocumentTests.cs` – Swagger flag (Constitution XII):
  `SwaggerDocument_WhenSwaggerIsEnabled_IsServed` (factory override `Swagger:Enabled=true` →
  `GET /swagger/v1/swagger.json` → `200`) and `SwaggerDocument_ByDefault_IsNotServed` (no
  override → `404`). Red: the template maps Swagger only in Development, and the factory runs in
  Production. `test:`
- [X] T078 In `src/Program.cs` replace the template's Development-only Swagger with
  `AddEndpointsApiExplorer()` + `AddSwaggerGen` (including the XML comment file) and
  `UseSwagger()`/`UseSwaggerUI()` mapped **only when `Swagger:Enabled` is `true`**, never tied to
  the Development environment; add `"Swagger": { "Enabled": false }` to `src/appsettings.json` and
  `"Swagger": { "Enabled": true }` to `src/appsettings.Development.json`. Green for T077. `feat:`
- [X] T079 Add to `OpenApiDocumentTests` – document content (FR-022, Constitution X):
  `POST /api/payments` documents `200` → `PaymentResponse`, `400` →
  `PaymentRejectedProblemDetails` (schema has `paymentStatus` and `errors`), `502` and `503` →
  `BankFailureProblemDetails` (schema has `errorCode`), `500` → `ProblemDetails`; `[Theory]` over
  `cardNumber`, `cvv`, `currency`, `amount`, `expiryMonth`, `expiryYear` → each property of the
  `PostPaymentRequest` schema has a non-empty `description`. Red. `test:`
- [X] T080 Add XML documentation comments to `PaymentsController`, `PaymentResponse`,
  `PaymentRejectedProblemDetails`, `BankFailureProblemDetails` (and confirm them on
  `IAcquiringBank`, `IPaymentRepository`), and to each `PostPaymentRequest` property stating its
  rule – because the rules live in `Domain/` and DataAnnotations on the DTO would break
  Constitution II, the rule text is repeated here on purpose (README design decision):
  `cardNumber` "Required. 14–19 characters, digits 0-9 only."; `expiryMonth` "Required. 1–12.";
  `expiryYear` "Required. Full year, not in the past, at most 9999; month + year must not be
  before the current month (UTC)."; `currency` "Required. Uppercase ISO 4217 code: GBP, EUR or
  USD."; `amount` "Required. Integer in the minor currency unit, at least 1 (USD $10.50 = 1050).";
  `cvv` "Required. 3–4 characters, digits 0-9 only." Add `[ProducesResponseType]` on
  `ProcessPaymentAsync` for `200` (`PaymentResponse`), `400` (`PaymentRejectedProblemDetails`),
  `502` and `503` (`BankFailureProblemDetails`), `500` (`ProblemDetails`). Green for T079. `docs:`
- [ ] T081 [UC1] Write `test/EndToEnd/SimulatorGatewayFactory.cs` (`WebApplicationFactory<Program>`
  with `AcquiringBank:BaseUrl` `http://localhost:8080` – the real simulator – the **real clock**
  (no `FakeTimeProvider`) and the **real logging** configuration, no fake logging) and
  `test/EndToEnd/ProcessPaymentJourneyTests.cs` with `[Trait("Category", "E2E")]`: card ending `7`
  → `200 Authorized`, ending `8` → `200 Declined`, ending `0` → `503 bank_unavailable` (expiry
  12/2030). **Verify**: `docker compose up -d bank_simulator` then
  `dotnet test --filter "Category=E2E"` passes, and `dotnet test --filter "Category!=E2E"` does not
  run it. `test:`
- [X] T082 [P] Create `src/PaymentGateway.Api.http` with `@base = http://localhost:5067` (comment:
  `http://localhost:8090` for compose) and requests for Authorized (card ending 7), Declined
  (ending 8), Rejected (card `1234`, currency `gbp`, amount `0`), Bank unavailable (ending 0),
  unknown route, and `GET {{base}}/health`. **Verify**: each request returns the status in
  quickstart §5. `docs:`
- [X] T083 [P] Rewrite `README.md` with the ten sections of the plan's "README plan" (overview; run
  locally; run with Docker; test commands per level and coverage; test strategy with the risks
  covered – card data leakage incl. framework logs, bank failure misreported as Declined,
  validation boundaries; Mermaid architecture and flow diagrams; API usage with 200/400/502/503
  examples; observability – events 1000–1003/2000–2001, `traceId`, the two instruments and the
  scope of `rejected`, `http.server.request.duration` for unreadable bodies **and for latency
  percentiles (SC-006, research R18)**, framework log levels and why, `/health`; design decisions
  and assumptions – including the request-field rules repeated in XML comments, payment ids as
  random v4 GUIDs (not guessable) and concurrency safety from an immutable `Payment` in a
  `ConcurrentDictionary` without a stress test (analysis E4); production next steps) plus "How
  this was built" linking `specs/` and the constitution. **Verify**: every command in it runs as
  written. `docs:`
- [ ] T084 Run the Definition of Done: `dotnet build -c Release` (0 warnings),
  `dotnet format --verify-no-changes`, `dotnet test --filter "Category!=E2E" --collect:"XPlat Code
  Coverage"`, the E2E run, and every scenario of [quickstart.md](quickstart.md) §1–§8 including the
  manual latency check of §5 (SC-006, research R18); fix any gap in the task that introduced it.
  **Verify**: all gates pass. `chore:`

---

## Dependencies & Execution Order

### Phase dependencies

| Phase | Depends on | Blocks |
|---|---|---|
| 1 Setup | – | everything |
| 2 Foundational | 1 | 3–9 |
| 3 Domain | 2 | 4, 6 |
| 4 ProcessPaymentService | 3 | 5 (`IAcquiringBank`, `BankAuthorizationResult`), 6, 7 |
| 5 Bank adapter | 2 (options), 4 | 6, 7 |
| 6 HTTP (US1 → US2 → US3) | 4, 5 | 7, 8, 9 |
| 7 Observability | 6 | 9 |
| 8 Packaging | 6 (T075 verifies a POST; T076 builds the image from T074) | 9 |
| 9 Docs and E2E | 6–8 (T077–T080 need only 6) | – |

### Story order inside the HTTP phase

US1 (T051–T058) is the MVP; US2 (T059–T062) and US3 (T063–T064) each extend
`PaymentResultMapper` and can be done in either order after US1.

### Within each pair

Test task (Red) → the implementation task(s) that follow it (Green) → refactor → commit. Never
start the next test before the current pair is green. T051 has three Green steps (T052–T054).

## Parallel Opportunities

- Phase 1: T002 and T003 (different project files).
- Phase 3: T015/T016 (`SupportedCurrencies`) alongside T017–T034 (`PaymentRequest`); T035 once
  T018 exists.
- Phase 4: T043 alongside T039–T042.
- Phase 5: T049 alongside T045–T048.
- Phase 6: T055 alongside T051–T054.
- Phase 8: T074 and T076 in parallel; T075 after T074.
- Phase 9: T077 → T078 → T079 → T080; T082 and T083 in parallel with T081.

### Parallel example (Phase 3)

```text
Developer A: T015 → T016  (SupportedCurrencies)
Developer B: T017 → T018 → T019 → … → T034  (PaymentRequest rules)
then:        T035 → T036  (Payment)
```

## Implementation Strategy

### MVP first

1. Phases 1–2 → foundation checkpoint.
2. Phases 3–5 → domain, use case and adapter proven in isolation.
3. Phase 6 US1 (T051–T058) → **STOP and validate** quickstart scenarios 1–3 against the simulator.

### Incremental delivery

4. US2 (T059–T062) → scenarios 4–6.
5. US3 (T063–T064) → scenarios 7–8.
6. Phase 7 → observability gates of the Definition of Done.
7. Phases 8–9 → Docker, CI, docs, E2E → full Definition of Done (T084).

## Notes

- Summary: 84 tasks – Setup 4, Foundational 10, Domain 22, Use case 8, Adapter 6, HTTP 14
  (US1 8, US2 4, US3 2), Observability 9, Packaging 3, Docs/E2E 8.
- Tasks naming a guard test (T031, T058, T073) may be green on first run because an earlier task
  already delivered the behaviour; the task says how the test is shown to fail when the behaviour
  is absent.
- Accepted without a task (analysis E3, E4): "a recorded payment is retrievable" (FR-018, SC-004)
  is closed by UC2's repository test and E2E journey; unguessable ids and concurrency safety are
  documented as design decisions (T083).
- SC-006 (latency) is verified manually, not by an automated test (research R18; quickstart §5;
  T084).
- UC2 (`specs/002-retrieve-payment`) builds on this list: `IPaymentRepository.GetById`, the `GET`
  action, its mapper cases and tests.

## Implementation notes (2026-09-27)

Deviations and findings recorded while implementing; the code and tests reflect them.

- **T010/T011** – `Microsoft.AspNetCore.Hosting.Diagnostics` is set to `None`: its request log scope
  carries `RequestPath`, which would print a pasted card number on every entry. T010 also asserts
  that category is off. Consequence: `Program.cs` registers an `ActivitySource` listener for
  `Microsoft.AspNetCore` so requests still get a trace id (research R15).
- **T005/T045** – the fixture keeps the application's default bank timeout (10 s); only timeout
  tests lower it to 1 s (`PaymentGatewayFactory.ShortBankTimeout`). A 1 s default made cold-start
  tests flaky.
- **T047/T063** – tests with a delayed bank response use their own WireMock server: WireMock records
  a delayed request only when its response completes, which polluted the next test's
  "no bank request" assertion on the shared server. The timeout test does not assert the call
  count for the same reason (the other failure cases do).
- **T072** – the mapper receives its logger by constructor injection (it is itself resolved from DI).
- **T073** – `LogText` ignores the random `TraceId`/`SpanId`/`ParentId` scope values when searching
  for card data: a 3-digit CVV occurred in them by chance.
- **T078/T080** – Swashbuckle upgraded 6.2.3 → 6.9.0: 6.2.3 omitted response types declared with
  `application/problem+json`.
- **T084** – `Properties/launchSettings.json` binds only `http://localhost:5067`: the template's
  HTTPS URL made `dotnet run` fail without a developer certificate, and the gateway serves HTTP only
  (TLS terminated upstream).
- **Pending Docker** (deferred by request): T074 image build, T075 `docker compose up`, T081 E2E run
  against the simulator, and the Docker parts of T084. Files are written; T081's tests compile and
  are excluded from the default run.

---

## Phase 10: Convergence

- [X] T085 CRITICAL – Make `dotnet format --verify-no-changes` pass: convert to CRLF line endings and add a final newline in `src/Application/IAcquiringBank.cs`, `src/Application/ProcessPaymentService.cs`, `src/Http/PaymentsController.cs`, `src/Infrastructure/AcquiringBankClient.cs`, `src/Infrastructure/AcquiringBankClient.Log.cs`, `test/Integration/AcquiringBankClientTests.cs`, `test/Integration/ProcessPaymentEndpointTests.cs`, `test/Unit/Application/ProcessPaymentServiceTests.cs`, `test/Unit/Domain/SupportedCurrenciesTests.cs`, `test/Unit/Fakes/FakeAcquiringBank.cs`, `test/Unit/Architecture/LayerDependencyTests.cs` and `test/Unit/Architecture/TypeDependencies.cs`, without editing `.editorconfig`. **Verify**: the command exits 0. `style:` per Constitution VI (contradicts)
- [X] T086 CRITICAL – Add XML documentation comments to the driven ports: a type-level `<summary>` on `src/Application/IAcquiringBank.cs`, and a type-level `<summary>` plus a `<summary>` on `Add` in `src/Application/IPaymentRepository.cs`, which has none. **Verify**: `dotnet build -c Release` shows 0 warnings. `docs:` per Constitution X (partial)
- [X] T087 [UC1] Add to `test/Integration/ProcessPaymentEndpointTests.cs` (US2) `[Theory]` `Post_WhenBodyIsUnreadable_LogsPaymentRequestUnreadableOnce`: `"amount":"ten"` and invalid JSON `{` each produce exactly **one** `PaymentRequestUnreadable` entry (EventId 1003, Information) with `invalidFields` holding the binding paths (e.g. `$.amount`) and no body content. The entry's `TraceId` scope value equals the response `traceId`. Red, because no 1003 entry is written today. `test:` per T071, Constitution XI (missing)
- [X] T088 [UC1] Add a `LoggerMessage` `PaymentRequestUnreadable` (EventId 1003, Information, `invalidFields`) in a partial file next to `src/Http/PaymentResultMapper.cs`. Inject `ILogger<PaymentResultMapper>` through the constructor and call it from `ToUnreadableBodyResult` with the `ModelState` keys only. Do not touch `PaymentGatewayMetrics`. Green for T087. `feat:` per T072, plan: data-model "Log events" (missing)
- [X] T089 [UC1] Write `test/Unit/Http/PaymentResultMapperTests.cs`: `ToUnreadableBodyResult_ForAnotherAction_ReturnsValidationProblemWithoutPaymentStatus`. Use an `ActionContext` whose `ControllerActionDescriptor.MethodInfo` is a method other than `PaymentsController.ProcessPaymentAsync`. Expect `400` `ValidationProblemDetails` with `traceId` and **no** `paymentStatus`. Add `ToUnreadableBodyResult_ForProcessPaymentAction_ReturnsPaymentRejected`, which expects `PaymentRejectedProblemDetails`. Red, because the factory returns Rejected for every action today. `test:` per T062, Constitution API Design 1.0.2 (partial)
- [X] T090 [UC1] Make `PaymentResultMapper.ToUnreadableBodyResult` action-aware. When `context.ActionDescriptor is ControllerActionDescriptor d && d.MethodInfo.Name == nameof(PaymentsController.ProcessPaymentAsync)`, return `PaymentRejectedProblemDetails` and write the 1003 log. Any other action gets a plain `ValidationProblemDetails`, with `traceId` applied through `Problem(...)`, and no `paymentStatus`. Green for T089. `feat:` per T062, plan: research R3 (partial)
- [X] T091 Record the `PaymentRuleSchemaFilter` decision (OpenAPI field descriptions come from `PaymentRequest.Messages` instead of per-property XML rule text, so the document and a Rejected response cannot drift) in README §9 "Design decisions and assumptions", and update the `PostPaymentRequest` entry in `specs/001-process-payment/data-model.md` to match. **Verify**: `OpenApiDocumentTests` stays green. `docs:` per T080, plan: data-model `PostPaymentRequest` (unrequested)

