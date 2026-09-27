# Feature Specification: Idempotent Payment Processing

**Feature Branch**: `feature/uc1-implementation` (no feature branch created; spec directory `specs/003-idempotent-payments`)

**Created**: 2026-09-27

**Status**: Draft – needs approval (see "Constitution Impact")

**Input**: User description: "Idempotent payment processing for POST /api/payments, modelled on
Checkout.com's idempotency (https://www.checkout.com/docs/developer-resources/api/idempotency) but
using the generic `Idempotency-Key` HTTP header. The merchant may send an optional Idempotency-Key
(V4 UUID recommended) so a retried payment request never creates a second payment. Without the
header, behaviour is unchanged. Only successful (200 Authorized or Declined) results are stored
against the key; a replay returns the same status code and body without calling the acquiring bank;
a 400 Rejected result is not stored; the same key with a different request is refused (422); a
concurrent request with a key still being processed gets 409 Conflict; keys expire after 72 hours.
Open point: 503 after a bank timeout. Keys in memory, global (no merchant authentication), no card
data stored in clear, header documented in OpenAPI."

**Extends**: UC1 – Process a payment ([`specs/001-process-payment`](../001-process-payment/spec.md)).
Stories below are labelled `[UC1]` in `tasks.md` because they change the UC1 endpoint; no new use
case is introduced (Constitution, Principle III).

**Reason for building it** (Constitution, Principle I): the assessment does not ask for idempotency.
It is added because a merchant whose payment request times out cannot know whether the shopper
was charged; without a way to retry safely, the only options are to risk a duplicate charge or to
abandon a payment that may have succeeded. The behaviour follows an established industry model
(Checkout.com) so merchants meet familiar rules.

## Clarifications

### Session 2026-09-27

- Q: Which header name? → A: **`Idempotency-Key`** – the generic name, with Checkout.com's
  behaviour, not their vendor-prefixed header.
- Q: How does this feature enter the project? → A: As its own Spec Kit feature (003), extending
  UC1.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Merchant retries a payment without charging the shopper twice (Priority: P1)

A merchant submits a payment with an idempotency key. The response is lost (network drop, client
timeout). The merchant resends the same request with the same key and receives the original
result – the same payment, with the same identifier and status – and the acquiring bank is not
contacted a second time.

**Why this priority**: preventing a duplicate charge is the whole purpose of the feature.

**Independent Test**: submit a valid payment with a key, submit it again with the same key, and
verify both responses are identical, only one payment exists and the bank received one request.

**Acceptance Scenarios**:

1. **Given** a payment was Authorized under key K, **When** the merchant resends the same request
   with key K, **Then** the gateway returns the same status code and body (same payment identifier,
   status `Authorized`) and does not contact the acquiring bank.
2. **Given** a payment was Declined under key K, **When** the merchant resends the same request with
   key K, **Then** the gateway returns the same Declined response and does not contact the bank.
3. **Given** a payment was processed under key K, **When** the merchant retrieves it by identifier
   (UC2), **Then** exactly one payment exists for that request.
4. **Given** a request without an idempotency key, **When** it is submitted twice, **Then** both are
   processed as separate payments, exactly as before this feature.

---

### User Story 2 - Merchant is protected from misusing a key (Priority: P2)

A key identifies one payment attempt. If the merchant reuses a key for a different payment, or
sends a second request while the first is still in progress, the gateway refuses it instead of
returning another payment's result or processing the payment twice.

**Why this priority**: without these guards a key could leak one payment's result to another
request, or two in-flight requests could both reach the bank.

**Independent Test**: reuse a key with a changed amount and verify the refusal; send two
simultaneous requests with the same key and verify one is processed and the other refused.

**Acceptance Scenarios**:

1. **Given** a payment was processed under key K, **When** a request with key K and a different
   amount (or any other field) is submitted, **Then** the gateway refuses it with
   `422 Unprocessable Entity`, does not contact the bank and does not return the stored payment.
2. **Given** a request with key K is still being processed, **When** another request with key K
   arrives, **Then** the gateway answers `409 Conflict`, does not contact the bank for it, and the
   first request completes normally.
3. **Given** an idempotency key that is empty, too long or contains characters outside the allowed
   set, **When** the request is submitted, **Then** it is Rejected, naming the `Idempotency-Key`
   header, and the bank is not contacted.

---

### User Story 3 - Merchant can correct a failed attempt under the same key (Priority: P3)

When no payment was made – the request was Rejected, or the bank refused the request – nothing is
stored under the key, so the merchant can correct the problem and retry with the same key.

**Why this priority**: follows the Checkout.com model and avoids forcing merchants to generate new
keys after a validation error.

**Independent Test**: submit an invalid request with key K, then a valid request with key K, and
verify the second is processed normally.

**Acceptance Scenarios**:

1. **Given** a request with key K was Rejected, **When** a corrected request with key K is
   submitted, **Then** it is validated and processed as a new attempt.
2. **Given** a request with key K ended in a bank error (`502`), **When** the same request with key
   K is resent, **Then** it is processed again and the bank is contacted again.
3. **Given** a key was stored more than 72 hours ago, **When** a request with that key is submitted,
   **Then** it is processed as a new attempt.

---

### Edge Cases

- **Bank timeout (`503 bank_unavailable`)**: the first attempt may have been authorized by the
  bank even though the gateway never received the answer. Behaviour on retry:
  [NEEDS CLARIFICATION: after a 503 caused by a timeout, should a retry with the same key
  (a) be processed again, exactly like Checkout.com – risking a duplicate charge because the
  acquiring bank has no idempotency key; (b) replay the stored 503 until the key expires, so the
  bank is never contacted twice for that key; or (c) be refused with 409 until the key expires?]
- **Bank unavailable (`503`) returned by the bank itself** (not a timeout): the bank answered, so no
  payment was made; treated like a bank error (not stored, retry processes again) unless the
  clarification above decides otherwise for all `503` responses.
- **Unreadable body with a key**: Rejected like any unreadable body; nothing stored under the key.
- **Same key, same fields, different JSON formatting or field order**: treated as the same request
  (requests are compared by their values, not by their bytes).
- **Key differing only in letter case**: a different key (keys are compared exactly).
- **Gateway restart**: all stored keys are lost, like all payments; a retry after a restart is
  processed as a new attempt (documented limitation).
- **Retrieval (`GET /api/payments/{id}`)**: ignores the header; retrieval is already safe to repeat.
- **Replayed response and card data**: the replayed body is the original body, which already holds
  only the last four digits; nothing else is stored.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `POST /api/payments` MUST accept an optional `Idempotency-Key` request header. When it
  is absent, processing MUST be exactly as specified by UC1.
- **FR-002**: A key MUST be 1–255 characters of visible ASCII (no spaces or control characters); a
  V4 UUID is recommended. An invalid key MUST make the request Rejected (`400`, `paymentStatus:
  "Rejected"`) with an error naming `Idempotency-Key`, without contacting the bank (Constitution
  Principle IX). Keys are compared exactly (case-sensitive).
- **FR-003**: When a request with a key ends in `200` (Authorized or Declined), the gateway MUST
  store the key with the request's fingerprint and the response, for **72 hours** from that moment.
- **FR-004**: A request whose key is stored and whose fingerprint matches MUST receive the stored
  status code and body, MUST NOT contact the acquiring bank and MUST NOT create a payment.
- **FR-005**: A request whose key is stored but whose fingerprint differs MUST be refused with
  `422 Unprocessable Entity`, MUST NOT contact the bank and MUST NOT reveal the stored payment.
- **FR-006**: While a request with a key is being processed, any other request with the same key
  MUST be refused with `409 Conflict` and MUST NOT contact the bank.
- **FR-007**: Requests that end `400` (Rejected, including an unreadable body) or `502` (bank error)
  MUST NOT be stored; a later request with the same key is processed as a new attempt.
- **FR-008**: The outcome after a `503` bank failure MUST follow the decision recorded for the
  "Bank timeout" edge case.
- **FR-009**: The fingerprint MUST be derived from every field of the payment request (card number,
  expiry month, expiry year, currency, amount, CVV) so any change is detected, and MUST NOT allow
  the card number or CVV to be recovered from what is stored (Constitution Principle VIII).
- **FR-010**: `409` and `422` responses MUST be ProblemDetails with `traceId` and an `errorCode`
  (`idempotency_key_in_use`, `idempotency_key_mismatch`), and MUST NOT carry `paymentStatus`,
  because no payment outcome was produced.
- **FR-011**: Each replay, mismatch and conflict MUST be logged with the key and the request's
  trace id, never card data (Constitution Principle XI). A replay MUST NOT be counted as a new
  payment outcome.
- **FR-012**: The OpenAPI document MUST describe the header (optional, format, 72-hour window) and
  the `409` and `422` responses of `POST /api/payments` (Constitution Principle X).
- **FR-013**: The README MUST describe the behaviour, the 503 decision and the in-memory,
  global-key limitations (Constitution Principle X).

### Key Entities

- **Idempotency record**: a key; a fingerprint of the request (no recoverable card data); the stored
  status code and body (already free of card data); the time it expires; whether the request is
  still in progress.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any number of retries of the same request with the same key within 72 hours,
  exactly one payment is created and the acquiring bank receives exactly one request.
- **SC-002**: 100% of replays return a body identical to the original response.
- **SC-003**: When two requests with the same key arrive together, the bank receives exactly one
  request and the other merchant request is told to retry later.
- **SC-004**: A key reused for a different payment never returns another payment's details.
- **SC-005**: No stored idempotency data, log entry or response contains a full card number or
  CVV (verified by tests).
- **SC-006**: Merchants that send no key see no change in behaviour (all UC1 tests still pass).

## Constitution Impact

The constitution (1.0.2) currently excludes this feature. Planning MUST NOT start until an
amendment is approved through `/speckit-constitution`:

- **Principle I** lists idempotency keys among features that "MUST NOT be built unless a spec
  records the reason and it is approved" – the reason is recorded above; approval is pending.
- **API Design** says "Idempotency is out of scope and documented as a production next step" – to be
  replaced by the rules of this spec, adding `409` and `422` to `POST /api/payments`.
- **Principle X** lists idempotency keys under README "Production next steps" – to be moved to the
  implemented behaviour, keeping persistent keys and per-merchant scope as next steps.
- **Principle VIII** ("A non-idempotent payment request MUST NOT be retried automatically") stays
  true: the gateway still never retries the bank on its own.

## Assumptions

- Keys are kept in memory next to payments and are lost on restart, as the assessment allows for
  payments.
- There is no merchant authentication, so keys are global rather than per merchant; per-merchant
  scope is a production next step.
- Expired keys are removed or ignored; the exact clean-up mechanism is a planning decision.
- The fingerprint uses a keyed or salted hash (or equivalent) so a stored value cannot be matched
  back to a card number by brute force; the exact method is a planning decision.
- Replays are not marked by a special response header, as Checkout.com does not document one.
- The 72-hour window matches Checkout.com's default and is a named constant.
