namespace BitcoinSwapper.Models;

/// <summary>
/// Represents information about a submarine swap
/// </summary>
public class SwapInfo
{
    public string Address { get; set; } = "";
    public ulong MinSats { get; set; }
    public ulong MaxSats { get; set; }
    public ulong FeeSats { get; set; }
}
