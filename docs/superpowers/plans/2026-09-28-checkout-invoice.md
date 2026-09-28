# Checkout and Invoice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Staff can close an active room session after receiving payment, then view and print one immutable invoice.

**Architecture:** Keep the existing MVC5 controller/service/EF6 structure. Checkout preview reads current data; Confirm owns one Serializable transaction and calls the existing BillingService calculation. Invoice stores final totals and snapshots needed for later printing.

**Tech Stack:** .NET Framework 4.7.2, ASP.NET MVC5, EF6, SQL Server, Razor.

**Spec:** `MusicBoxManagement_ProjectPlan_v1.3.md`, sections 26–28, 35, 46, 47, 53, 55.

## Global Constraints

- Only `Cash` or `BankTransfer`; one paid Invoice per RoomSession.
- Confirm calculates money and checkout time on the server; preview is read-only.
- On successful Confirm, cancel Pending orders and complete Session/Reservation in the same transaction.
- Keep historical room, item, employee-name and price snapshots; Vietnamese UI and Vietnam time.
- No new payment service, QR gateway, PDF service, or extra workflow status.

## Review Focus

- Repeated Confirm must return the existing invoice and leave its money/payment method unchanged.
- Concurrent Order/Checkout must not leave a new active order on a completed session.
- A stale preview must not determine the final amount.
- A failed write must not partly complete the session or cancel pending orders.
- Walk-in sessions without Reservation must also complete.

---

### Task 1: Invoice schema and migration

**Files:** `Models/Invoice.cs`, `Models/IdentityModels.cs`, generated `Migrations/*_AddInvoice.*`, project file.

**Interfaces:** `ApplicationDbContext.Invoices`; Invoice fields and constraints as spec section 35.

- [ ] Add Invoice model and EF mappings, including unique Session and InvoiceNumber.
- [ ] Scaffold migration with EF6 CLI; add check constraints for amounts and PaymentMethod.
- [ ] Build, apply migration to isolated SQL Server database, verify constraints.
- [ ] Commit with a Vietnamese message.

### Task 2: Checkout transaction

**Files:** `Services/CheckoutService.cs`, `Services/BillingService.cs`, `Tests/Checkout.Integration.ps1`.

**Interfaces:** `CheckoutService.GetPreview(int)` returns BillingPreview; `Confirm(int, string, string)` returns invoice id/result.

- [ ] Write integration test for preview read-only, final amount recalculation, Pending cancellation, session/reservation completion and repeated Confirm; run red.
- [ ] Implement short Serializable Confirm transaction with BillingService.Calculate and one audit entry.
- [ ] Run tests green on isolated SQL Server and commit.

### Task 3: Checkout and invoice UI

**Files:** `Controllers/CheckoutController.cs`, `Controllers/InvoicesController.cs`, related view models/views, RoomSession details/layout, project file and CSS.

**Interfaces:** Checkout GET/POST protected by Session.CheckOut; invoice list/details by Invoice.View; print by Invoice.Print.

- [ ] Add checkout preview/confirm screen and staff navigation.
- [ ] Add invoice search, details and browser print view using snapshots.
- [ ] Build, precompile Razor, smoke test public and protected routes, review, then commit.
