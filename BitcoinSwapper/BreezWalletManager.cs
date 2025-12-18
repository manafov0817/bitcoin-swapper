using NBitcoin;
using Breez.Sdk;
using Serilog;

namespace BitcoinSwapper;

public class BreezWalletManager
{
    private BlockingBreezServices? _sdk;
    private string? _workingDir;
    private Mnemonic? _mnemonic;

    public BreezWalletManager()
    {
        _workingDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".breez-wallet"
        );
        Directory.CreateDirectory(_workingDir);
    }

    /// <summary>
    /// Generates a new BIP39 seed phrase (12 words)
    /// </summary>
    public string GenerateSeedPhrase()
    {
        _mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
        return _mnemonic.ToString();
    }

    /// <summary>
    /// Initializes wallet from seed phrase
    /// </summary>
    public async Task InitializeWallet(string seedPhrase)
    {
        Log.Information("Initializing wallet from seed phrase");

        // Validate seed phrase
        try
        {
            _mnemonic = new Mnemonic(seedPhrase, Wordlist.English);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Invalid seed phrase", ex);
        }

        // Configure Breez SDK
        var config = new Config(
            breezserver: "https://bs1.breez.technology",
            chainnotifierUrl: "https://chainnotifier.breez.technology",
            mempoolspaceUrl: "https://mempool.space/api",
            workingDir: _workingDir,
            network: Network.Bitcoin,
            paymentTimeoutSec: 60,
            defaultLspId: "LSP_LNBITS_BREEZ",
            apiKey: null,
            maxfeePercent: 0.5
        );

        // Create seed from mnemonic
        var seed = _mnemonic.DeriveSeed();

        Log.Information("Connecting to Breez SDK...");

        try
        {
            // Connect to Breez SDK
            _sdk = await Task.Run(() => BreezSdkMethods.Connect(
                config: config,
                seed: seed,
                listener: new BreezEventListener()
            ));

            Log.Information("✅ Connected to Breez SDK successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to connect to Breez SDK");
            throw new Exception("Failed to initialize wallet. Check your internet connection.", ex);
        }
    }

    /// <summary>
    /// Gets current balance
    /// </summary>
    public async Task<BalanceInfo> GetBalance()
    {
        EnsureInitialized();

        var nodeInfo = await Task.Run(() => _sdk!.NodeInfo());

        return new BalanceInfo
        {
            LightningSats = nodeInfo.channelsBalanceMsat / 1000,
            OnchainSats = 0, // Breez primarily uses Lightning
            TotalSats = nodeInfo.channelsBalanceMsat / 1000,
            TotalBtc = (nodeInfo.channelsBalanceMsat / 1000) / 100_000_000.0
        };
    }

    /// <summary>
    /// Creates a Lightning invoice to receive payment
    /// </summary>
    public async Task<string> CreateInvoice(ulong? amountSats = null, string? description = null)
    {
        EnsureInitialized();

        var amountMsat = amountSats.HasValue ? amountSats.Value * 1000 : 0;

        var request = new ReceivePaymentRequest(
            amountMsat: amountMsat,
            description: description ?? "Bitcoin Swapper Payment"
        );

        var response = await Task.Run(() => _sdk!.ReceivePayment(request));

        return response.lnInvoice.bolt11;
    }

    /// <summary>
    /// Sends payment via Lightning invoice or on-chain address
    /// </summary>
    public async Task<PaymentResult> SendPayment(string destination, ulong? amountSats = null)
    {
        EnsureInitialized();

        // Parse the input (could be invoice or address)
        var parseResult = await Task.Run(() => _sdk!.ParseInput(destination));

        if (parseResult is InputType.Bolt11 bolt11Input)
        {
            // Lightning payment
            var amountMsat = amountSats.HasValue ? amountSats.Value * 1000 : null;

            var request = new SendPaymentRequest(
                bolt11: bolt11Input.invoice.Bolt11,
                amountMsat: amountMsat
            );

            var response = await Task.Run(() => _sdk!.SendPayment(request));

            return new PaymentResult
            {
                PaymentHash = response.payment.id,
                AmountSats = response.payment.amountMsat / 1000,
                FeeSats = response.payment.feeMsat / 1000,
                Status = "Success"
            };
        }
        else if (parseResult is InputType.BitcoinAddress addressInput)
        {
            // On-chain payment requires reverse swap
            if (!amountSats.HasValue)
                throw new ArgumentException("Amount required for on-chain payments");

            return await SendToOnchain(destination, amountSats.Value);
        }
        else
        {
            throw new ArgumentException("Invalid payment destination");
        }
    }

    /// <summary>
    /// Gets on-chain address for receiving (submarine swap)
    /// </summary>
    public async Task<SwapInfo> GetReceiveOnchainAddress()
    {
        EnsureInitialized();

        var request = new ReceiveOnchainRequest();
        var response = await Task.Run(() => _sdk!.ReceiveOnchain(request));

        return new SwapInfo
        {
            Address = response.BitcoinAddress,
            MinSats = response.MinAllowedDeposit,
            MaxSats = response.MaxAllowedDeposit,
            FeeSats = 0 // Fee is percentage-based
        };
    }

    /// <summary>
    /// Sends to on-chain address (reverse swap)
    /// </summary>
    public async Task<PaymentResult> SendToOnchain(string address, ulong amountSats)
    {
        EnsureInitialized();

        var request = new SendOnchainRequest(
            amountSat: amountSats,
            onchainRecipientAddress: address,
            pairHash: "",
            satPerVbyte: 1
        );

        var response = await Task.Run(() => _sdk!.SendOnchain(request));

        return new PaymentResult
        {
            PaymentHash = "",
            TxId = "",
            AmountSats = amountSats,
            FeeSats = 0,
            Status = "Pending"
        };
    }

    /// <summary>
    /// Gets transaction history
    /// </summary>
    public async Task<List<TransactionInfo>> GetTransactions(int limit = 20)
    {
        EnsureInitialized();

        var request = new ListPaymentsRequest();
        var payments = await Task.Run(() => _sdk!.ListPayments(request));

        return payments
            .Take(limit)
            .Select(p => new TransactionInfo
            {
                Type = p.PaymentType.ToString(),
                AmountSats = p.AmountMsat / 1000,
                FeeSats = p.FeeMsat / 1000,
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(p.PaymentTime).DateTime,
                Status = p.Status.ToString(),
                IsIncoming = p.PaymentType == PaymentType.Received
            })
            .ToList();
    }

    /// <summary>
    /// Disconnects from Breez SDK
    /// </summary>
    public async Task Disconnect()
    {
        if (_sdk != null)
        {
            await Task.Run(() => _sdk.Disconnect());
            _sdk = null;
            Log.Information("Disconnected from Breez SDK");
        }
    }

    private void EnsureInitialized()
    {
        if (_sdk == null)
            throw new InvalidOperationException("Wallet not initialized. Call InitializeWallet first.");
    }
}

/// <summary>
/// Event listener for Breez SDK events
/// </summary>
public class BreezEventListener : EventListener
{
    public override void OnEvent(BreezEvent e)
    {
        Log.Information("Breez Event: {EventType}", e.GetType().Name);

        switch (e)
        {
            case BreezEvent.InvoicePaid invoicePaid:
                Log.Information("Invoice paid: {PaymentHash}", invoicePaid.Details.Payment.Id);
                break;
            case BreezEvent.PaymentSucceed paymentSucceed:
                Log.Information("Payment succeeded: {PaymentHash}", paymentSucceed.Details.Id);
                break;
            case BreezEvent.PaymentFailed paymentFailed:
                Log.Error("Payment failed: {Error}", paymentFailed.Details.Error);
                break;
            case BreezEvent.Synced synced:
                Log.Information("Synced");
                break;
        }
    }
}

// Data models
public class BalanceInfo
{
    public ulong LightningSats { get; set; }
    public ulong OnchainSats { get; set; }
    public ulong TotalSats { get; set; }
    public double TotalBtc { get; set; }
}

public class PaymentResult
{
    public string PaymentHash { get; set; } = "";
    public string TxId { get; set; } = "";
    public ulong AmountSats { get; set; }
    public ulong FeeSats { get; set; }
    public string Status { get; set; } = "";
}

public class SwapInfo
{
    public string Address { get; set; } = "";
    public ulong MinSats { get; set; }
    public ulong MaxSats { get; set; }
    public ulong FeeSats { get; set; }
}

public class TransactionInfo
{
    public string Type { get; set; } = "";
    public ulong AmountSats { get; set; }
    public ulong FeeSats { get; set; }
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "";
    public bool IsIncoming { get; set; }
}
