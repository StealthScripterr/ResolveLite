namespace ResolveLite.Core.Documents;

/// <summary>
/// The full lifecycle, defined now so the contract never changes later.
///   Pending → Processing → Ready → Superseded
///                       ↘ Failed → (safe retry) → Pending
/// Phase 1 only ever writes <see cref="Pending"/>.
/// </summary>
public enum DocumentStatus
{
    Pending,
    Processing,
    Ready,
    Failed,
    Superseded
}
