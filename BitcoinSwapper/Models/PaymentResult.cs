namespace BitcoinSwapper.Models;

/// <summary>
/// Represents the result of a payment operation
/// </summary>
public class PaymentResult
{
    public string PaymentHash { get; set; } = "";
    public string TxId { get; set; } = "";
    public ulong AmountSats { get; set; }
    public ulong FeeSats { get; set; }
    public string Status { get; set; } = "";
}
