# Bitcoin Swapper - Lightning Network Trading with Seed Phrase

A simple C# console application for buying/selling Bitcoin programmatically using **Breez SDK** and **seed phrase** authentication. This solution requires **minimal Bitcoin amounts** and offers **extremely low fees** via the Lightning Network.

## Features

✅ **Seed Phrase Based** - Full control with BIP39 mnemonic (12 words)
✅ **No Infrastructure Hosting** - Breez SDK handles Lightning node
✅ **Minimal Amounts** - Trade satoshis (pennies)
✅ **Low Fees** - Lightning Network fees (<1 sat per transaction)
✅ **Instant Payments** - Lightning-fast transactions
✅ **Automatic Swaps** - On-chain ↔ Lightning conversion
✅ **Non-Custodial** - Your keys, your coins

## How It Works

This application uses **Breez SDK**, which provides:
- **Lightning Network wallet** controlled by your seed phrase
- **Greenlight** infrastructure (Lightning node runs remotely, you control keys)
- **Submarine swaps** (on-chain → Lightning)
- **Reverse swaps** (Lightning → on-chain)
- **LSP (Lightning Service Provider)** for automatic channel management

## Prerequisites

- **.NET 8.0 SDK** or higher
- Internet connection
- Basic understanding of Bitcoin/Lightning

## Installation

### 1. Install .NET SDK

**Ubuntu/Debian:**
```bash
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
```

**macOS:**
```bash
brew install dotnet@8
```

**Windows:**
Download from [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)

### 2. Clone and Build

```bash
cd bitcoin-swapper/BitcoinSwapper
dotnet restore
dotnet build
```

## Usage

### Run the Application

```bash
dotnet run
```

### First Time Setup

**Option 1: Create New Wallet**
```
1. Choose "Create new wallet"
2. Save your 12-word seed phrase securely
3. NEVER share this with anyone!
```

**Option 2: Restore Existing Wallet**
```
1. Choose "Restore wallet from seed phrase"
2. Enter your 12 or 24-word seed phrase
```

### Main Operations

#### 1. Check Balance
```
Shows your Lightning and on-chain balance in satoshis and BTC
```

#### 2. Receive Bitcoin
```
- Generates a Lightning invoice
- Share invoice with sender
- Supports any amount or fixed amounts
```

#### 3. Send Bitcoin
```
- Pay Lightning invoices
- Send to on-chain addresses (via reverse swap)
- Ultra-low fees
```

#### 4. Swap Between On-Chain and Lightning

**On-chain → Lightning (Submarine Swap):**
```
- Get a Bitcoin address
- Send BTC to that address
- Automatically appears in Lightning balance
- Min: ~50,000 sats
- Max: ~4,000,000 sats
```

**Lightning → On-chain (Reverse Swap):**
```
- Provide Bitcoin address
- Specify amount
- Lightning balance → on-chain BTC
- Takes 10-60 minutes for confirmations
```

## Code Examples

### Programmatic Usage

```csharp
using BitcoinSwapper;

// Create wallet manager
var wallet = new BreezWalletManager();

// Generate new seed phrase
var mnemonic = wallet.GenerateSeedPhrase();
Console.WriteLine($"Save this: {mnemonic}");

// Initialize from seed phrase
await wallet.InitializeWallet(mnemonic);

// Check balance
var balance = await wallet.GetBalance();
Console.WriteLine($"Balance: {balance.TotalSats} sats");

// Create invoice to receive 10,000 sats
var invoice = await wallet.CreateInvoice(10000, "Payment for service");
Console.WriteLine($"Invoice: {invoice}");

// Send payment
var result = await wallet.SendPayment("lnbc...", amountSats: 5000);
Console.WriteLine($"Paid {result.AmountSats} sats, fee: {result.FeeSats} sats");

// Swap on-chain to Lightning
var swapInfo = await wallet.GetReceiveOnchainAddress();
Console.WriteLine($"Send BTC to: {swapInfo.Address}");

// Swap Lightning to on-chain
var swapResult = await wallet.SendToOnchain(
    "bc1q...",
    amountSats: 100000
);

// Get transactions
var txs = await wallet.GetTransactions(limit: 10);
foreach (var tx in txs)
{
    Console.WriteLine($"{tx.Type}: {tx.AmountSats} sats");
}

// Cleanup
await wallet.Disconnect();
```

## Trading Strategies

### For Buy/Sell Bitcoin

Since this is a **Lightning wallet**, not a traditional exchange, "trading" works differently:

**To "Buy" Bitcoin:**
1. Buy Bitcoin from exchange (Coinbase, Binance, etc.)
2. Withdraw to your Breez on-chain address (submarine swap)
3. Automatically converts to Lightning balance
4. Now you can spend instantly with minimal fees

**To "Sell" Bitcoin:**
1. Send Lightning balance to on-chain (reverse swap)
2. Send on-chain BTC to exchange
3. Sell for fiat

**For Active Trading:**
- Use exchange APIs (Binance, Kraken) for price trading
- Use this wallet for custody and low-fee transfers

## Minimum Amounts

- **Lightning payments**: As low as **1 satoshi** (fraction of a cent)
- **Submarine swap**: ~**50,000 sats minimum** (~$20 at current prices)
- **Reverse swap**: ~**50,000 sats minimum**
- **Transaction fees**: **< 1,000 sats** (under $0.50)

Compare to on-chain:
- On-chain fee: $1-$20 depending on network congestion
- Lightning fee: $0.001-$0.01 (100x cheaper!)

## Security Best Practices

🔐 **Seed Phrase Security:**
- Store in password manager (encrypted)
- Write on paper, store in safe
- Never share with anyone
- Never store in plain text files
- Consider metal backup for large amounts

🔐 **Operational Security:**
- Don't store large amounts in hot wallet
- Test with small amounts first
- Use hardware wallet for long-term storage
- Keep software updated

## Troubleshooting

### "Failed to connect to Breez SDK"
- Check internet connection
- Verify firewall isn't blocking
- Try again in a few minutes

### "Invalid seed phrase"
- Ensure 12 or 24 words
- Check spelling (BIP39 wordlist)
- Remove extra spaces

### "Insufficient balance"
- Check balance with option 1
- Need minimum ~50k sats for swaps
- Receive funds first

### Swap taking too long
- Submarine swaps: Wait for Bitcoin confirmations (10-60 min)
- Reverse swaps: Wait for on-chain confirmations
- Check transaction history

## Project Structure

```
BitcoinSwapper/
├── BitcoinSwapper.csproj    # Project dependencies
├── Program.cs               # Main application (interactive menu)
├── BreezWalletManager.cs   # Breez SDK wrapper
└── README.md               # This file
```

## Dependencies

- **BreezSdk** (0.5.1) - Lightning Network SDK
- **NBitcoin** (7.0.37) - Bitcoin library for BIP39
- **Newtonsoft.Json** (13.0.3) - JSON handling
- **Serilog** (3.1.1) - Logging framework

## How Breez SDK Works

Breez SDK uses **Greenlight** architecture:
1. Your seed phrase derives private keys
2. Greenlight runs Lightning node remotely
3. You sign all transactions locally (non-custodial)
4. No need to run 24/7 infrastructure
5. LSP manages liquidity and channels automatically

## Alternatives Considered

| Solution | Fees | Min Amount | Hosting | Seed Phrase |
|----------|------|------------|---------|-------------|
| **Breez SDK** ✅ | <$0.01 | 1 sat | No | Yes |
| Exchange API | 0.1-0.5% | $10-20 | No | No |
| On-chain only | $1-20 | Any | No | Yes |
| Full LN node | <$0.01 | 1 sat | Yes (24/7) | Yes |

## Resources

- [Breez SDK Documentation](https://sdk-doc.breez.technology/)
- [Lightning Network Overview](https://lightning.network/)
- [BIP39 Specification](https://github.com/bitcoin/bips/blob/master/bip-0039.mediawiki)
- [Submarine Swaps Explained](https://docs.lightning.engineering/the-lightning-network/multihop-payments/submarine-swaps)

## License

MIT License - Use at your own risk

## Disclaimer

⚠️ **IMPORTANT:**
- This is experimental software
- Test with small amounts first
- Not financial advice
- You are responsible for securing your seed phrase
- Lightning Network is still evolving technology
- Loss of seed phrase = loss of funds (unrecoverable)

## Support

For issues with:
- **Breez SDK**: https://github.com/breez/breez-sdk
- **This code**: Create an issue in this repository
- **Bitcoin/Lightning**: https://bitcoin.stackexchange.com/

---

**Built with Breez SDK** ⚡
Non-custodial Lightning made simple
