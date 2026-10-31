namespace Hakathon.Server.Database;

public class Customer
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Plan { get; set; }
}

public class Charge
{
    public required string Id { get; set; }
    public required string CustomerId { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset ChargedAt { get; set; }
    public required string Description { get; set; }
    public ChargeStatus Status { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
}

public enum ChargeStatus
{
    Paid,
    Refunded
}

public class Ticket
{
    public required string Id { get; set; }
    public required string CustomerId { get; set; }
}

public class AuditEvent
{
    public int Id { get; set; }
    public required string TicketId { get; set; }
    public DateTimeOffset At { get; set; }
    public required string Step { get; set; }
    public required string Detail { get; set; }
}

// Дія, яку policy не дозволила виконати автоматично; її погоджує або відхиляє support-агент.
public class PendingAction
{
    public required string Id { get; set; }
    public required string TicketId { get; set; }
    public required string Type { get; set; }
    public string? ChargeId { get; set; }
    public decimal? Amount { get; set; }
    public required string Rule { get; set; }
    public PendingActionStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
}

public enum PendingActionStatus
{
    Pending,
    Approved,
    Rejected
}
