using NBitcoin;
using Breez.Sdk;
using Serilog;
using BitcoinSwapper.Models;
using BitcoinSwapper.Listeners;

namespace BitcoinSwapper.Services;

/// <summary>
/// Manages Bitcoin wallet operations using Breez SDK
/// </summary>
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
        var nodeConfig = new NodeConfig.Greenlight(
            config: new GreenlightNodeConfig(
                partnerCredentials: null,
                inviteCode: null
            )
        );

        var config = new Config(
            breezserver: "https://bs1.breez.technology",
            chainnotifierUrl: "https://chainnotifier.breez.technology",
            mempoolspaceUrl: "https://mempool.space/api",
            workingDir: _workingDir,
            network: Breez.Sdk.Network.Bitcoin,
            paymentTimeoutSec: 60,
            defaultLspId: "LSP_LNBITS_BREEZ",
            apiKey: null,
            maxfeePercent: 0.5,
            exemptfeeMsat: 5000,
            nodeConfig: nodeConfig
        );

        // Create seed from mnemonic
        var seedBytes = _mnemonic.DeriveSeed();
        var seed = new List<byte>(seedBytes);

        Log.Information("Connecting to Breez SDK...");

        try
        {
            // Connect to Breez SDK
            var connectRequest = new ConnectRequest(
                config: config,
                seed: seed
            );

            _sdk = await Task.Run(() => BreezSdkMethods.Connect(
                req: connectRequest,
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
            LightningSats = (ulong)(nodeInfo.channelsBalanceMsat / 1000),
            OnchainSats = 0, // Breez primarily uses Lightning
            TotalSats = (ulong)(nodeInfo.channelsBalanceMsat / 1000),
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
        var parseResult = await Task.Run(() => BreezSdkMethods.ParseInput(destination));

        if (parseResult is InputType.Bolt11 bolt11Input)
        {
            // Lightning payment
            ulong? amountMsat = amountSats.HasValue ? (ulong?)(amountSats.Value * 1000) : null;

            var request = new SendPaymentRequest(
                bolt11: bolt11Input.invoice.bolt11,
                useTrampoline: false,
                amountMsat: amountMsat,
                label: null
            );

            var response = await Task.Run(() => _sdk!.SendPayment(request));

            return new PaymentResult
            {
                PaymentHash = response.payment.id,
                AmountSats = (ulong)(response.payment.amountMsat / 1000),
                FeeSats = (ulong)(response.payment.feeMsat / 1000),
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
            Address = response.bitcoinAddress,
            MinSats = (ulong)response.minAllowedDeposit,
            MaxSats = (ulong)response.maxAllowedDeposit,
            FeeSats = 0 // Fee is percentage-based
        };
    }

    /// <summary>
    /// Sends to on-chain address (reverse swap)
    /// </summary>
    public async Task<PaymentResult> SendToOnchain(string address, ulong amountSats)
    {
        EnsureInitialized();

        // Prepare the onchain payment first
        var prepareRequest = new PrepareOnchainPaymentRequest(
            amountSat: amountSats,
            amountType: SwapAmountType.Receive,
            claimTxFeerate: 1
        );

        var prepareResponse = await Task.Run(() => _sdk!.PrepareOnchainPayment(prepareRequest));

        // Execute the payment
        var payRequest = new PayOnchainRequest(
            recipientAddress: address,
            prepareRes: prepareResponse
        );

        var payResponse = await Task.Run(() => _sdk!.PayOnchain(payRequest));

        return new PaymentResult
        {
            PaymentHash = "",
            TxId = "",
            AmountSats = amountSats,
            FeeSats = (ulong)payResponse.reverseSwapInfo.onchainAmountSat,
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
                Type = p.paymentType.ToString(),
                AmountSats = (ulong)(p.amountMsat / 1000),
                FeeSats = (ulong)(p.feeMsat / 1000),
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(p.paymentTime).DateTime,
                Status = p.status.ToString(),
                IsIncoming = p.paymentType == PaymentType.Received
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
