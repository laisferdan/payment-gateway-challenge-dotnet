# Feature Specification: Retrieve a Payment's Details (UC2)

**Feature Branch**: `feature/uc2-implementation` (spec directory `specs/002-retrieve-payment`)

**Created**: 2026-09-26

**Status**: Draft

**Input**: User description: "UC2 – Retrieve a payment's details. Read docs/requirements/assessment.md
('Requirements' item 2, 'Retrieving a payment's details', 'Note: Payment Storage'),
.specify/memory/constitution.md and specs/001-process-payment/ first."

**Use case**: UC2 – Retrieve a payment's details (Constitution, Principle III). Stories below are
labelled `[UC2]` in `tasks.md`. UC1 – Process a payment is specified in
[`specs/001-process-payment`](../001-process-payment/spec.md) and is not changed by this feature.

**Source**: `docs/requirements/assessment.md` – "Requirements" item 2, "Retrieving a payment's
details", "Note: Payment Storage". Each requirement below cites the assessment section or
constitution principle it traces to (Constitution, Principle I).

**Dependency**: UC1 records every Authorized and Declined payment under its identifier
(UC1 FR-018, FR-019). UC2 reads those records; it creates, changes and deletes nothing.

## Clarifications

### Session 2026-09-26

- Q: Should a malformed identifier be refused as invalid input or answered as "payment not
  found"? → A: Refused as **invalid input naming the `id` field**, never "not found": the merchant
  must be able to tell a typo from a missing payment (fail-closed, Constitution Principle IX).
- Q: Which textual forms of the identifier are accepted? → A: **Any GUID notation the platform
  parses** – canonical 8-4-4-4-12, without hyphens, in braces `{…}`, in parentheses `(…)` or the
  hexadecimal `{0x…,…}` form. The assessment says "feel free to choose whatever format"; a GUID
  written in another notation is the same identifier, not an invalid value coerced into validity.
  *(Refined 2026-09-27)*: surrounding whitespace is **not** a notation – the platform's parser
  trims it, which Principle IX forbids, so an otherwise valid GUID with leading or trailing
  whitespace is refused.
- Q: Does the letter case of the identifier matter? → A: **No.** GUID hex digits are
  case-insensitive by definition, so an uppercase identifier finds the same payment as its
  lowercase form. This is not coercion.
- Q: Which response codes are used for retrieval? → A: **`200 OK`** found; **`404 Not Found`**
  payment not found; **`400 Bad Request`** invalid identifier. Errors are ProblemDetails with
  `traceId`. The `400` for an invalid identifier does **not** carry `paymentStatus: "Rejected"`,
  because no payment was attempted. (Plan note: UC1's invalid-model response must add
  `paymentStatus` only on the payment-processing route.)
- Q: Is a retrieval metric needed, and how are retrieval outcomes observed? → A: **No retrieval
  metric.** Each retrieval outcome (found, not found, invalid identifier) is logged with the
  request's trace id. Monitoring for identifier enumeration is a production next step. No
  constitution change is needed for metrics.
- Q: How is API documentation completed across UC1 and UC2? → A: UC1 FR-022 and UC2 FR-017
  together complete it; UC2's Definition of Done includes documenting the retrieval endpoint in the
  OpenAPI document and the README.
- Q: The assessment's prose asks for a "masked card number", but its field table lists only "Last
  four card digits" – which applies? → A: **The table.** The response carries the last four digits
  as text and no other masked form of the card number. Recorded as an assumption for the README's
  "Design Decisions & Assumptions".

## User Scenarios & Testing *(mandatory)*

All stories below belong to use case **UC2** and are labelled `[UC2]` in `tasks.md`.

### User Story 1 - Merchant retrieves a previously processed payment (Priority: P1)

A merchant needs to reconcile its sales and produce reports. It takes the payment identifier it
received when the payment was processed (UC1) and asks the gateway for that payment. The gateway
returns the payment's details – identifier, status (**Authorized** or **Declined**), last four
card digits, expiry month, expiry year, currency and amount – exactly as they were returned when
the payment was processed, and never the full card number or CVV.

**Why this priority**: this is the whole of the assessment's second requirement; without it the
merchant cannot reconcile or report on payments.

**Independent Test**: record an Authorized and a Declined payment (via UC1, or directly in the
payment store), retrieve each by its identifier, and verify every field equals the value
returned when the payment was processed and that neither the full card number nor the CVV
appears.

**Acceptance Scenarios**:

1. **Given** a payment that was processed and Authorized, **When** the merchant retrieves it by
   its identifier, **Then** the merchant receives the identifier, status `Authorized`, the last
   four card digits, expiry month, expiry year, currency and amount, each identical to the
   processing response for that payment.
2. **Given** a payment that was processed and Declined, **When** the merchant retrieves it,
   **Then** the merchant receives status `Declined` with the same set of fields, identical to the
   processing response.
3. **Given** a payment whose card number ends in digits with leading zeros (e.g. `0012`), **When**
   the merchant retrieves it, **Then** the last four digits are returned as `0012`.
4. **Given** any retrieved payment, **When** the merchant inspects the response, **Then** it
   contains neither the full card number nor the CVV, nor any other masked form of the card
   number – only the last four digits.
5. **Given** a payment has been retrieved once, **When** the merchant retrieves it again any number
   of times, **Then** every response is identical, the payment is unchanged and the acquiring bank
   was never contacted.
6. **Given** a recorded payment, **When** the merchant retrieves it using its identifier in
   uppercase, without hyphens, in braces, in parentheses or in the hexadecimal `{0x…}` form,
   **Then** the merchant receives the same payment, and the returned identifier is in the same
   form as in the processing response.
7. **Given** the gateway and the acquiring bank simulator are running, **When** a merchant
   processes a payment and then retrieves it with the identifier returned (end-to-end journey),
   **Then** the retrieved details equal the processing response.

---

### User Story 2 - Merchant is told when a payment cannot be found (Priority: P2)

A merchant asks for a payment using an identifier the gateway has no record of – a typo that
still forms a valid identifier, an identifier from another system, a request that was Rejected
or failed at the bank and so never became a payment, or a payment lost because the gateway
restarted. The gateway answers clearly that the payment was not found, in the same error format
merchants already receive from payment processing, with a trace identifier they can quote to
support.

**Why this priority**: merchants will inevitably look up identifiers that do not exist; a clear,
consistent answer is required for reconciliation, but it only matters once P1 works.

**Independent Test**: retrieve a well-formed identifier that was never issued and verify the
merchant receives the "payment not found" outcome, with a trace identifier, and without any
payment details.

**Acceptance Scenarios**:

1. **Given** a well-formed identifier that no recorded payment has, **When** the merchant retrieves
   it, **Then** the merchant is told the payment was not found, in the same error format family as
   UC1 errors, including the request's trace identifier.
2. **Given** a payment request that was Rejected or ended in a bank failure (UC1), **When** the
   merchant tries to retrieve any identifier, **Then** no such payment exists and the outcome is
   "payment not found" (such requests never received an identifier).
3. **Given** a payment was recorded and the gateway has since restarted, **When** the merchant
   retrieves it, **Then** the outcome is "payment not found".
4. **Given** any "payment not found" response, **When** the merchant inspects it, **Then** it is
   the same for every unknown identifier and does not reveal whether that identifier ever existed
   or anything about other payments.

---

### User Story 3 - Merchant is told when the identifier is not valid (Priority: P3)

A merchant sends something that is not a payment identifier at all (e.g. `abc`, `123`, a
truncated identifier). The gateway refuses it as invalid input, naming the `id` field, so the
merchant can tell a malformed identifier from a payment that does not exist – rather than
looking it up or failing unexpectedly.

**Why this priority**: fail-closed input validation is a constitutional rule (Principle IX) and
helps merchants spot integration bugs quickly, but it is less common than the not-found case.

**Independent Test**: retrieve with several malformed identifiers and verify each gets the
"invalid identifier" outcome naming the `id` field, never "payment not found", never a payment and
never an unexpected error.

**Acceptance Scenarios**:

1. **Given** an identifier that is not a GUID in any accepted text form, **When** the merchant
   retrieves it, **Then** the merchant is told the input is invalid, the explanation names the
   `id` field and the rule it broke, and the payment store is not searched.
2. **Given** an invalid identifier, **When** the merchant reads the explanation, **Then** it uses
   the same error format family as UC1 errors, includes the request's trace identifier, and does
   not state that a payment was Rejected (no payment was attempted).
3. **Given** an invalid identifier, **When** the response is returned, **Then** the outcome is
   distinct from "payment not found" and is never an unexpected error.

---

### Edge Cases

- **Declined payment**: retrievable exactly like an Authorized one; status `Declined`.
- **Leading-zero last four digits** (e.g. `0012`, `0000`): returned as text with the zeros intact.
- **Uppercase vs lowercase identifier**: the same payment; letter case is not significant.
- **Identifier without hyphens, in braces `{…}`, in parentheses `(…)` or in the hexadecimal
  `{0x…}` form**: accepted as the same identifier and finds the same payment.
- **Identifier with a wrong length, non-hex characters (e.g. `g`), misplaced hyphens or mismatched
  brackets**: invalid identifier naming `id`.
- **Otherwise valid identifier with leading or trailing whitespace**: invalid identifier naming
  `id` – whitespace is refused, never trimmed into validity (Constitution Principle IX).
- **Empty identifier or only whitespace**: never treated as "retrieve all payments", never a
  payment and never an unexpected error; the merchant receives an error response (see Notes for
  `/speckit-plan`).
- **The all-zeros identifier** (`00000000-0000-0000-0000-000000000000`): well-formed but never
  issued, so "payment not found".
- **Identifier of a Rejected request or a bank failure**: none was ever issued; any lookup is
  "payment not found".
- **Payment retrieved immediately after it was processed**: the processing response is only
  returned after the payment is recorded (UC1 acceptance scenario 1.3), so it is retrievable at
  once.
- **Many retrievals at the same time, and retrievals while payments are being processed**: every
  retrieval returns either the complete recorded payment or "not found" (for a payment not yet
  recorded) – never a partial or mixed record – and processing is not slowed or disturbed.
- **Gateway restarted**: all previously recorded payments are "not found" (in-memory storage).
- **Acquiring bank unavailable**: retrieval is unaffected, since it never contacts the bank.

## Requirements *(mandatory)*

### Functional Requirements

**Retrieval** *(Assessment: Requirements 2; Retrieving a payment's details)*

- **FR-001**: Merchants MUST be able to retrieve a previously made payment by its payment
  identifier.
- **FR-002**: A retrieved payment MUST include: identifier, status (`Authorized` or `Declined`),
  last four card digits, expiry month, expiry year, currency and amount in the minor currency
  unit. It MUST NOT include any other form of the card number (the "masked card number" in the
  assessment's prose is satisfied by the last four digits listed in its field table).
- **FR-003**: The retrieved payment MUST have exactly the same fields and values as the UC1
  processing response for that payment, so merchants see one consistent representation of a
  payment. *(Assessment: the two response tables are identical; UC1 FR-016.)*
- **FR-004**: Only payments that reached a bank decision (`Authorized` or `Declined`, recorded by
  UC1 FR-018) MUST be retrievable. Requests that were Rejected or ended in a bank failure never
  received an identifier and cannot be retrieved. *(Assessment: Requirements 1 and 2; UC1 FR-012,
  FR-017.)*

**Identifier validation** *(Assessment: Id – "feel free to choose whatever format"; Constitution
Principle IX)*

- **FR-005**: The payment identifier supplied by the merchant MUST be validated before use. An
  identifier that is not a GUID in one of the notations of FR-006, exactly as sent, MUST be
  refused as **invalid input** with an explanation naming the `id` field and the rule it broke. The payment store MUST
  NOT be searched, the outcome MUST be distinguishable from "payment not found", and it MUST NOT be
  an unexpected error.
- **FR-006**: Every GUID notation the platform parses MUST be accepted as the same identifier –
  canonical 8-4-4-4-12, without hyphens, in braces, in parentheses or the hexadecimal `{0x…}`
  form – and letter case MUST NOT be significant. Reading a GUID written in another notation is
  interpreting the same value, not coercing an invalid one. The identifier MUST have no leading
  or trailing whitespace: the platform's parser would trim it, which is altering a value into
  validity (Constitution Principle IX). Any other value is refused (FR-005), never altered into
  validity.

**Not found** *(Assessment: Retrieving a payment's details; Constitution API Design)*

- **FR-007**: When a well-formed identifier matches no recorded payment, the gateway MUST tell the
  merchant the payment was not found, using the same error format family as UC1 errors
  (ProblemDetails carrying the request's trace identifier).
- **FR-008**: The "payment not found" response MUST be identical in form for every unknown
  identifier and MUST NOT reveal whether the identifier ever existed, nor any detail of any other
  payment.

**Behaviour** *(Assessment: Retrieving a payment's details; Note: Payment Storage)*

- **FR-009**: Retrieval MUST have no side effects: it MUST NOT change, create or delete any
  payment, and MUST return the same result every time for the same identifier while the gateway
  is running.
- **FR-010**: Retrieval MUST NOT contact the acquiring bank; it answers from the gateway's own
  payment records.
- **FR-011**: Payments MUST be read from the in-memory payment store populated by UC1; no
  database or external storage is introduced. Payments recorded before a gateway restart are
  "not found" afterwards. *(Assessment: Note: Payment Storage; Constitution Technical Constraints.)*
- **FR-012**: Retrievals MUST be correct under concurrency: simultaneous retrievals, and
  retrievals concurrent with payment processing, MUST each return either the complete recorded
  payment or "not found" – never a partial record.

**Access** *(Constitution Principle I – authentication out of scope)*

- **FR-013**: Any caller that presents a payment's identifier MAY retrieve it; there is no merchant
  ownership check. Enumeration is limited by identifiers being random and not guessable (UC1
  FR-019). Merchant-scoped access and monitoring for identifier enumeration are recorded as
  production next steps.

**Card data protection** *(Assessment: "serious compliance risk"; Constitution Principle VIII)*

- **FR-014**: The full card number and the CVV MUST NOT appear in any retrieval response, log
  entry or error message; only the last four card digits may be exposed.
- **FR-015**: The last four card digits MUST be returned as text so leading zeros are preserved.
  *(UC1 FR-021.)*

**Observability** *(Constitution Principle XI)*

- **FR-016**: Every retrieval outcome – found, not found, invalid identifier – MUST be logged with
  the request's trace identifier, which also appears in every error response returned to the
  merchant. No retrieval metric is added; the processing-outcome metrics of UC1 are unchanged.

**Documentation** *(Constitution Principle X)*

- **FR-017**: The retrieval capability MUST be described in the gateway's published API
  documentation and in the README (API usage, and the design decisions recorded in this spec),
  including the identifier and its accepted forms, every returned field, and every possible
  outcome (found, not found, invalid identifier, unexpected error). Together with UC1 FR-022 this
  completes the API documentation.

### Key Entities

- **Payment** (from UC1, read-only here): identifier, status (`Authorized` or `Declined`), last four
  card digits, expiry month, expiry year, currency, amount (minor units). Holds no full card
  number, CVV or authorization code.
- **Payment Identifier**: the random, non-sequential GUID issued by UC1 when a payment is
  recorded; the only lookup key. Accepted in any GUID notation of FR-006, case-insensitively and
  without surrounding whitespace; returned in the same form as in the processing response.
- **Retrieval Outcome**: exactly one of **Found** (the payment), **Not found**, or **Invalid
  identifier** (field `id` and the rule broken).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of payments recorded by UC1 (Authorized and Declined) are retrievable by their
  identifier while the gateway runs, with every field identical to the processing response.
- **SC-002**: 0 occurrences of a full card number or CVV in any retrieval response, log entry or
  error message across the full test suite.
- **SC-003**: 100% of well-formed unknown identifiers produce "payment not found", and 100% of
  malformed identifiers produce "invalid identifier" – 0 unexpected errors across all tested
  identifier forms.
- **SC-004**: 95% of retrievals are answered in under 500 ms.
- **SC-005**: 0 acquiring bank calls and 0 changes to recorded payments are caused by retrievals.
- **SC-006**: Every retrieval outcome can be located in the gateway's logs using the trace
  identifier of the request.
- **SC-007**: A developer new to the gateway can retrieve a payment they processed using only the
  published API documentation, without reading the code.

## Notes for `/speckit-plan`

- **Testing**: the end-to-end merchant journey **process, then retrieve** against the real bank
  simulator (deferred from UC1, research R14) – for an Authorized and a Declined payment –
  verifying that the retrieved details equal the processing response.
- **Identifier validation tests** cover every accepted form (canonical, no hyphens, braces,
  parentheses, hexadecimal, uppercase) and invalid boundaries (wrong length, non-hex character,
  misplaced hyphen, mismatched bracket, surrounding whitespace) – Constitution Principle IX.
- **Card data**: absence of the full card number and CVV from retrieval logs is verified by a
  test (Constitution Principle VIII).
- **Invalid id must not become 404**: the chosen mechanism must turn a malformed id into the
  invalid-input outcome; a routing constraint that makes malformed ids unmatched routes would
  yield "not found" instead, contradicting FR-005.
- **`paymentStatus` only on processing**: UC1's shared invalid-model response must add
  `paymentStatus: "Rejected"` only on the payment-processing route (Clarifications, Q4).
- **Empty or whitespace-only identifier**: decide and document the resulting response (an empty
  path segment may not reach the retrieval action at all), keeping it an error that is never a
  payment, a list or an unexpected error.
- **Surrounding whitespace** *(resolved 2026-09-27)*: refused as an invalid identifier – the
  platform's GUID parser tolerates it, but Principle IX forbids trimming a value into validity
  (research R1; Edge Cases).
- **Concurrency (FR-012)**: state how it is verified – relying on the thread-safe store's
  guarantee, or one concurrent test (UC1 research R14 has no repository concurrency test).

## Assumptions

- **Retrievable payments**: only Authorized and Declined payments; Rejected requests and bank
  failures were never recorded and are therefore "not found" (FR-004).
- **One representation**: the retrieval response has the same fields and values as the UC1
  processing response (FR-003); last four digits are text (FR-015).
- **Masked card number**: the assessment's prose mentions "a masked card number", but its field
  table lists only "Last four card digits". The response follows the table – the last four digits
  as text and no other masked form (FR-002). Recorded in the README's "Design Decisions &
  Assumptions".
- **Not found format**: ProblemDetails with trace identifier, the same error format family as UC1
  (FR-007); it does not reveal whether an identifier ever existed (FR-008).
- **Malformed identifier**: refused as invalid input naming the `id` field (fail-closed,
  Constitution Principle IX), distinct from "not found" so the merchant can tell a typo from a
  missing payment (FR-005).
- **Identifier forms and letter case**: any GUID notation the platform parses is accepted, letter
  case is not significant, and surrounding whitespace is refused rather than trimmed (FR-006);
  the assessment leaves the format to the gateway.
- **Read-only and bank-free**: retrieval has no side effects and never contacts the acquiring bank
  (FR-009, FR-010).
- **No ownership check**: merchant authentication is out of scope (Constitution Principle I);
  anyone with the identifier can retrieve the payment. Random identifiers (UC1 FR-019) limit
  enumeration. Merchant-scoped access and enumeration monitoring are production next steps
  (README).
- **Observability**: every retrieval outcome is logged with the trace id; no retrieval metric
  (FR-016).
- **Volatile storage**: payments live in memory only and are lost when the gateway restarts; they
  are then "not found" (FR-011). Persistent storage is a production next step.
- **No listing or search**: retrieval is by identifier only; listing, filtering or searching
  payments (e.g. by date or merchant reference) is out of scope.

## Observations on UC1 Artifacts and the Constitution (all resolved)

Recorded when this spec was written; each has since been resolved outside this feature or by its
plan. Kept as history.

1. **Constitution API Design** – *resolved by constitution 1.0.2*: `GET /api/payments/{id}` now
   lists `400 Bad Request` (malformed id, `errors.id`, no `paymentStatus`).
2. **UC1 contract** – *resolved*: UC2's own [`contracts/payments-api.yaml`](contracts/payments-api.yaml)
   describes the retrieval operation and `$ref`s UC1's schemas; UC1's file is unchanged (research
   R10).
3. **UC1 `400` meaning** – *resolved*: `InvalidModelStateResponder` adds `paymentStatus:
   "Rejected"` only on the processing action (`[RespondsToInvalidRequest]`); the OpenAPI document
   and README distinguish the two `400`s (research R4).
4. **Concurrency (FR-012)** – *resolved*: verified by design and review, not a stress test
   (research R7).
5. **Metrics** – *resolved*: no retrieval metric; Constitution Principle XI needs no change.
6. **Documentation** – *resolved*: UC1 FR-022 and UC2 FR-017 together complete the API
   documentation.
