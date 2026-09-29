# Feature Specification: Process a Payment (UC1)

> **Superseded in part** – this records the plan. Where the code differs, the code, the README and
> the constitution are current; see [what changed since the plan](../README.md).

**Feature Branch**: `develop` (no feature branch created; spec directory `specs/001-process-payment`)

**Created**: 2026-09-26

**Status**: Draft

**Input**: User description: "see @docs/requirements/assessment.md and @.specify/memory/constitution.md"

**Use case**: UC1 – Process a payment (Constitution, Principle III). UC2 – Retrieve a payment's
details is specified separately.

**Source**: `docs/requirements/assessment.md` – "Requirements" item 1, "Processing a payment",
"Bank simulator". Each requirement below cites the assessment section it traces to
(Constitution, Principle I).

## Clarifications

### Session 2026-09-26

- Q: What format must currency codes have? → A: Exactly 3 characters, all uppercase; any other
  form (e.g. `gbp`, `GB`) is Rejected. Supported list stays `GBP`, `EUR`, `USD`.
- Q: Is the expiry rule (valid until the end of the expiry month) correct? → A: Yes, confirmed.
- Q: What form must the amount have? → A: An integer in the minor currency unit (USD: $0.01 is
  supplied as `1`, $10.50 as `1050`).
- Q: Is the "could not be processed" outcome for bank failures acceptable? → A: Yes, but the
  failure must be classified so the merchant can tell what kind of bank failure occurred.
- Q: What is in scope? → A: The payment gateway only; the acquiring bank is simulated to test
  the full payment flow. Keep the solution simple and maintainable, with no over-engineering and
  no authentication or real storage.
- Q: Should an amount of zero or a negative amount be accepted, or must the amount be greater
  than zero? → A: Must be greater than zero; `0` and negative amounts are Rejected.
- Q: When the bank fails, which kinds of failure should the merchant be able to tell apart? →
  A: Two kinds, following the bank's responses: **Bank unavailable** (bank answers 503 Service
  Unavailable, or does not answer in time) and **Bank error** (bank answers 400 Bad Request, or
  its answer cannot be read). Bank failures are error responses, not payment statuses, and are
  translated for the merchant in one consistent place (a single result-to-response mapping,
  defined in research R5).
- Q: What response does the merchant get for an Authorized or Declined payment? → A: `200 OK`
  with no `Location` header; the outcome is carried in the `status` field (`Authorized` |
  `Declined`), mirroring the acquiring bank's `200 OK` for both, and the merchant uses the `id`
  in the body to retrieve the payment later (UC2). The assessment does not ask for
  resource-creation semantics.

## User Scenarios & Testing *(mandatory)*

All stories below belong to use case **UC1** and are labelled `[UC1]` in `tasks.md`.

### User Story 1 - Merchant submits a valid payment and receives the bank's decision (Priority: P1)

A merchant's shopper checks out. The merchant submits the shopper's card details, the currency
and the amount to the payment gateway. The gateway checks the request, forwards it to the
acquiring bank, and returns the bank's decision – **Authorized** or **Declined** – together with
a payment identifier and a safe summary of the payment (last four card digits, expiry month,
expiry year, currency, amount). The payment is recorded so the merchant can look it up later.

**Why this priority**: taking money from shoppers is the reason the gateway exists; without this
journey there is no product.

**Independent Test**: submit a valid payment for a card the bank authorizes and one it declines;
verify each response carries the correct status, an identifier, the last four digits and the
submitted payment details, and never the full card number or CVV.

**Acceptance Scenarios**:

1. **Given** a valid payment request whose card the acquiring bank authorizes, **When** the
   merchant submits it, **Then** the merchant receives status `Authorized`, a new unique payment
   identifier, the last four card digits, the expiry month, the expiry year, the currency and the
   amount exactly as submitted.
2. **Given** a valid payment request whose card the acquiring bank declines, **When** the merchant
   submits it, **Then** the merchant receives status `Declined` with the same set of fields.
3. **Given** a payment was Authorized or Declined, **When** the response is returned, **Then** the
   payment has been recorded under its identifier with the same details and status, so it can be
   retrieved later (UC2).
4. **Given** any Authorized or Declined response, **When** the merchant inspects it, **Then** it
   contains neither the full card number nor the CVV.
5. **Given** a card number starting with zeros (e.g. last four digits `0012`), **When** the payment
   is processed, **Then** the last four digits are returned with their leading zeros intact.
6. **Given** two valid payment requests, **When** both are processed, **Then** each receives a
   different payment identifier.

---

### User Story 2 - Merchant is told why an invalid payment was rejected (Priority: P2)

A merchant submits a request with missing or invalid information (e.g. an expired card, an
unsupported currency, a CVV with letters). The gateway refuses it **without contacting the
acquiring bank**, returns status **Rejected**, and lists every problem found so the merchant can
fix the request in one go. No payment is created.

**Why this priority**: validation is the gateway's first responsibility in the assessment and
protects the bank, the merchant and the shopper; it builds on P1's request but is independently
valuable.

**Independent Test**: submit requests that each break one validation rule, plus one that breaks
several; verify each is Rejected, lists every failing field, never reaches the bank, and is not
recorded.

**Acceptance Scenarios**:

1. **Given** a request that breaks any validation rule in FR-003 to FR-010, **When** the merchant
   submits it, **Then** the merchant receives status `Rejected` and an explanation naming each
   invalid field and the rule it broke.
2. **Given** a request with several invalid fields, **When** the merchant submits it, **Then** all
   invalid fields are reported in a single response.
3. **Given** a rejected request, **When** the response is returned, **Then** the acquiring bank was
   never contacted and no payment was recorded (no identifier is issued).
4. **Given** a rejected request, **When** the merchant reads the explanation, **Then** it never
   contains the submitted card number or CVV.
5. **Given** a request whose body cannot be understood at all (e.g. not a structured payment
   request, or a field of the wrong type such as text for the amount), **When** the merchant
   submits it, **Then** it is also rejected without contacting the bank.

---

### User Story 3 - Merchant is told when the bank cannot process the payment (Priority: P3)

A merchant submits a valid payment, but the acquiring bank is unavailable, does not answer in
time, or returns an unexpected response. The gateway tells the merchant the payment could not be
processed right now, **without** claiming it was Declined, and does not record it as a processed
payment. The merchant can decide whether to try again.

**Why this priority**: bank outages are rare but must never be misreported as a decline, which
would mislead the merchant and the shopper; the happy path (P1) and validation (P2) come first.

**Independent Test**: submit a valid payment while the bank is unavailable (simulated by a card
number ending in `0`) and while the bank refuses the request (400 Bad Request); verify the
merchant gets a bank failure classified as **Bank unavailable** or **Bank error** respectively,
with no payment status, and that nothing was recorded.

**Acceptance Scenarios**:

1. **Given** a valid request and a bank that answers "service unavailable" (simulator: card
   number ending in `0`), **When** the merchant submits it, **Then** the merchant is told the
   payment could not be processed, classified as **Bank unavailable**, and no payment status
   (`Authorized`, `Declined`, `Rejected`) is returned.
2. **Given** the bank did not respond within the allowed time, **When** the merchant submits a
   valid request, **Then** the outcome is the same as scenario 1 (**Bank unavailable**).
3. **Given** the bank answers that the request it received is invalid (simulator: 400 Bad
   Request), **When** the merchant submits a valid request, **Then** the merchant is told the
   payment could not be processed, classified as **Bank error**, and it is not `Declined`.
4. **Given** the bank returns an answer the gateway cannot read, **When** the merchant submits a
   valid request, **Then** the outcome is the same as scenario 3 (**Bank error**).
5. **Given** any of the failures above, **When** the response is returned, **Then** no payment was
   recorded, the gateway made exactly one attempt to contact the bank, and the response contains
   neither the full card number nor the CVV.

---

### Edge Cases

- **Expiry in the current month**: a card expiring this month (e.g. 09/2026 on 2026-09-26) is
  still valid and is accepted; a card that expired last month is Rejected.
- **Expiry year in the future but month in the past of that year**: validity is decided on the
  month + year combination, not on each field alone.
- **Card number with spaces, dashes or letters**: Rejected (numeric characters only; no silent
  clean-up).
- **Card number of 13 or 20 digits**: Rejected; 14 and 19 digits are accepted.
- **CVV of 2 or 5 digits, or with letters**: Rejected; 3 and 4 digits are accepted.
- **CVV or card number with leading zeros**: accepted and preserved as submitted.
- **Currency in lowercase (`gbp`) or not in the supported list (`JPY`)**: Rejected.
- **Amount of zero, negative, fractional (`10.5`) or not a number**: Rejected.
- **Amount at the upper limit of what the gateway can represent**: accepted; beyond it, Rejected.
- **Expiry month 0 or 13**: Rejected; 1 and 12 are accepted.
- **Expiry year 9999 or 10000**: 9999 is accepted; 10000 is Rejected. The year must fit the
  bank contract's `MM/yyyy` format and the gateway's date handling, so an out-of-range year is
  always a Rejected outcome, never an unexpected error.
- **Missing field or empty value** for any required field: Rejected.
- **Unknown extra fields** in the request: ignored; they do not affect the outcome.
- **Bank declines a card whose number ends in an even digit, authorizes an odd one, and is
  unavailable for one ending in `0`** (simulator behaviour): gateway outcome follows the bank.
- **Many merchants submitting at the same time**: every payment is processed and recorded
  independently, each with its own identifier.

## Requirements *(mandatory)*

### Functional Requirements

**Processing** *(Assessment: Requirements 1; Processing a payment)*

- **FR-001**: Merchants MUST be able to submit a payment request containing card number, expiry
  month, expiry year, currency, amount and CVV.
- **FR-002**: Every payment request MUST end in exactly one outcome visible to the merchant: a
  payment with status `Authorized` or `Declined`, a `Rejected` response, or a classified bank
  failure (FR-017). The only statuses are `Authorized`, `Declined` and `Rejected`; a bank failure
  is an error, not a status.

**Validation** *(Assessment: Processing a payment – validation rules; Constitution Principle IX)*

- **FR-003**: Card number MUST be present, 14 to 19 characters long, and contain only numeric
  characters.
- **FR-004**: Expiry month MUST be present and a whole number from 1 to 12.
- **FR-005**: Expiry year MUST be present, a whole number that is not in the past, and MUST NOT
  exceed 9999.
- **FR-006**: The combination of expiry month and expiry year MUST NOT be before the current
  month (the card is valid until the end of its expiry month).
- **FR-007**: Currency MUST be present, exactly 3 characters, all uppercase, and one of the
  supported ISO 4217 codes: `GBP`, `EUR`, `USD`.
- **FR-008**: Amount MUST be present, an integer in the minor currency unit (USD: $0.01 = `1`,
  $10.50 = `1050`), and greater than zero; `0` and negative amounts are Rejected.
- **FR-009**: CVV MUST be present, 3 or 4 characters long, and contain only numeric characters.
- **FR-010**: Inputs MUST NOT be altered to make them valid (no trimming, padding, case
  conversion, rounding or defaulting); an invalid value is Rejected as submitted.
- **FR-011**: When a request fails validation, the gateway MUST return status `Rejected` with a
  list of every invalid field and the rule each one broke, in a single response.
- **FR-012**: A Rejected request MUST NOT be sent to the acquiring bank, MUST NOT be recorded, and
  MUST NOT receive a payment identifier.
- **FR-013**: A request that cannot be read as a payment request (malformed body, wrong data types)
  MUST be refused without contacting the bank, using the same error format as FR-011.

**Bank interaction and response** *(Assessment: Processing a payment – response fields; Bank
simulator)*

- **FR-014**: A valid request MUST be forwarded to the acquiring bank exactly once, with the card
  number, expiry (month and year), currency, amount and CVV.
- **FR-015**: When the bank authorizes the payment, the gateway MUST return status `Authorized`;
  when the bank declines it, `Declined`.
- **FR-016**: Authorized and Declined responses MUST include: a unique payment identifier, the
  status, the last four card digits, expiry month, expiry year, currency and amount.
- **FR-017**: When the bank fails, the gateway MUST tell the merchant the payment could not be
  processed and classify the failure as one of:
  - **Bank unavailable** – the bank answers that it is unavailable (503 Service Unavailable) or
    does not answer in time; trying again later may succeed.
  - **Bank error** – the bank answers that the forwarded request is invalid (400 Bad Request) or
    returns an answer the gateway cannot read; trying again will not help.

  In both cases the gateway MUST NOT report the payment as `Declined` or `Rejected`, MUST NOT
  record it, and MUST NOT retry automatically. Every bank failure MUST be translated into the
  merchant response in the same consistent way, using the same error format family
  (ProblemDetails) as FR-011.

**Recording** *(Assessment: Requirements 2 – enables retrieval; Payment storage note)*

- **FR-018**: Every Authorized or Declined payment MUST be recorded with its identifier, status,
  last four card digits, expiry month, expiry year, currency and amount, so it can be retrieved
  later by its identifier.
- **FR-019**: Payment identifiers MUST be unique and not guessable from other payments'
  identifiers.

**Card data protection** *(Assessment: "serious compliance risk"; Constitution Principle VIII)*

- **FR-020**: The full card number and the CVV MUST NOT appear in any response, recorded payment,
  log entry or error message; only the last four card digits may be exposed.
- **FR-021**: Last four card digits, card number and CVV MUST be handled as text so leading zeros
  are preserved.

**Documentation** *(Constitution Principle X)*

- **FR-022**: The payment processing capability MUST be described in the gateway's published API
  documentation, including every field, its constraints, and every possible outcome.

### Key Entities

- **Payment Request**: what the merchant submits – card number, expiry month, expiry year,
  currency, amount (minor units), CVV. Exists only while the request is being handled; never
  recorded as submitted.
- **Payment**: a payment the acquiring bank has decided on – identifier, status (`Authorized` or
  `Declined`), last four card digits, expiry month, expiry year, currency, amount. Recorded and
  later retrievable (UC2).
- **Payment Status**: `Authorized` or `Declined` for recorded payments; `Rejected` is an outcome
  of a request that never became a payment.
- **Validation Error**: a field name and the rule it broke; returned only for Rejected requests.
- **Bank Failure**: the reason a valid request could not be processed – `Bank unavailable` or
  `Bank error`; returned to the merchant, never recorded, and not a payment status.
- **Supported Currency**: one of `GBP`, `EUR`, `USD`.
- **Acquiring Bank**: the external party that authorizes or declines a payment; may be
  unavailable.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of requests breaking any validation rule are Rejected, and none of them reach
  the acquiring bank or are recorded.
- **SC-002**: 100% of Rejected responses list every invalid field of the request, so a merchant
  can correct any request after a single rejection.
- **SC-003**: 0 occurrences of a full card number or CVV in any response, recorded payment, log
  or error message across the full test suite.
- **SC-004**: 100% of Authorized and Declined outcomes match the acquiring bank's decision, and
  every one of them is retrievable afterwards by its identifier.
- **SC-005**: 0 bank failures (unavailable, timeout, unexpected answer) are reported to the
  merchant as Declined or recorded as payments.
- **SC-006**: When the acquiring bank answers promptly, 95% of merchants receive the outcome of a
  payment in under 2 seconds; Rejected outcomes are returned in under 1 second.
- **SC-007**: A developer new to the gateway can submit a first successful payment using only
  the published API documentation, without reading the code.

## Assumptions

- **Supported currencies**: `GBP`, `EUR` and `USD` – three widely used ISO 4217 codes, all with
  two minor-unit digits (satisfies "no more than 3 currency codes"; GBP matches the simulator
  example). Codes must be uppercase as defined by ISO 4217.
- **Expiry**: a card is valid until the last day of its expiry month, so the current month is
  accepted; "now" is the gateway's current date in UTC.
- **Expiry year** is expected as a full year (e.g. 2027), at most 9999 (FR-005). A two-digit
  year (e.g. `27`) is in the past and is therefore Rejected by FR-006.
- **Amount** must be greater than zero (confirmed in Clarifications); a shopper cannot pay a zero
  or negative amount. The upper limit is the largest integer the gateway can represent.
- **Bank failure outcome**: the assessment is silent on bank failures. They are reported as a
  classified error (**Bank unavailable** or **Bank error**, see FR-017), not as a fourth payment
  status. The response codes and the single error-handling component that produces them are
  defined in research R1 and R4 (see also R5).
- **No automatic retries**: retrying a payment request could charge the shopper twice; the
  merchant decides whether to resubmit.
- **Bank timeout**: a configurable time limit applies to the bank call (default 10 seconds,
  1–60 allowed – research R6).
- **Authorization code** returned by the bank is not part of the merchant response in the
  assessment and is not exposed.
- **Out of scope** (Constitution Principle I): merchant authentication, duplicate detection,
  refunds, captures, partial payments, currency conversion, card-scheme
  (e.g. Luhn) checks, and persistent storage – payments are kept in memory only, as allowed by
  the assessment, and are lost when the gateway restarts.
- **Dependency**: the acquiring bank simulator (`docker compose up`) represents the acquiring
  bank; its behaviour (odd = authorized, even = declined, `0` = unavailable) is used in tests.
- **Retrieval** of recorded payments is UC2 and is specified separately; this feature only
  ensures Authorized and Declined payments are recorded.
