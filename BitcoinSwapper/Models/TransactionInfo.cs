namespace BitcoinSwapper.Models;

/// <summary>
/// Represents a transaction in the payment history
/// </summary>
public class TransactionInfo
{
    public string Type { get; set; } = "";
    public ulong AmountSats { get; set; }
    public ulong FeeSats { get; set; }
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "";
    public bool IsIncoming { get; set; }
}
