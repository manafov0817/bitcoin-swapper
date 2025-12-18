namespace BitcoinSwapper.Models;

/// <summary>
/// Represents wallet balance information
/// </summary>
public class BalanceInfo
{
    public ulong LightningSats { get; set; }
    public ulong OnchainSats { get; set; }
    public ulong TotalSats { get; set; }
    public double TotalBtc { get; set; }
}
