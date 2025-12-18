using BitcoinSwapper;
using Serilog;

// Setup logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

Console.WriteLine("=== Bitcoin Swapper with Breez SDK ===\n");

try
{
    // Initialize wallet manager
    var walletManager = new BreezWalletManager();

    Console.WriteLine("Choose an option:");
    Console.WriteLine("1. Create new wallet (generate seed phrase)");
    Console.WriteLine("2. Restore wallet from seed phrase");
    Console.WriteLine("3. Exit");
    Console.Write("\nYour choice: ");

    var choice = Console.ReadLine();

    switch (choice)
    {
        case "1":
            await CreateNewWallet(walletManager);
            break;
        case "2":
            await RestoreWallet(walletManager);
            break;
        case "3":
            Console.WriteLine("Goodbye!");
            return;
        default:
            Console.WriteLine("Invalid choice");
            return;
    }

    // Main menu loop
    await RunMainMenu(walletManager);
}
catch (Exception ex)
{
    Log.Error(ex, "Fatal error occurred");
    Console.WriteLine($"\nError: {ex.Message}");
}
finally
{
    Log.CloseAndFlush();
}

static async Task CreateNewWallet(BreezWalletManager manager)
{
    Console.WriteLine("\n=== Creating New Wallet ===");

    var mnemonic = manager.GenerateSeedPhrase();

    Console.WriteLine("\n🔐 IMPORTANT: Save this seed phrase securely!");
    Console.WriteLine("════════════════════════════════════════");
    Console.WriteLine(mnemonic);
    Console.WriteLine("════════════════════════════════════════");
    Console.WriteLine("\nPress any key when you've saved it...");
    Console.ReadKey();

    await manager.InitializeWallet(mnemonic);
    Console.WriteLine("\n✅ Wallet created successfully!");
}

static async Task RestoreWallet(BreezWalletManager manager)
{
    Console.WriteLine("\n=== Restore Wallet ===");
    Console.Write("Enter your 12 or 24 word seed phrase: ");
    var mnemonic = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(mnemonic))
    {
        Console.WriteLine("Invalid seed phrase");
        return;
    }

    await manager.InitializeWallet(mnemonic);
    Console.WriteLine("\n✅ Wallet restored successfully!");
}

static async Task RunMainMenu(BreezWalletManager manager)
{
    while (true)
    {
        Console.WriteLine("\n=== Main Menu ===");
        Console.WriteLine("1. Check Balance");
        Console.WriteLine("2. Get Receiving Address/Invoice");
        Console.WriteLine("3. Send Bitcoin (Lightning)");
        Console.WriteLine("4. Swap (On-chain ↔ Lightning)");
        Console.WriteLine("5. View Transaction History");
        Console.WriteLine("6. Exit");
        Console.Write("\nYour choice: ");

        var choice = Console.ReadLine();

        try
        {
            switch (choice)
            {
                case "1":
                    await ShowBalance(manager);
                    break;
                case "2":
                    await GetReceivingInfo(manager);
                    break;
                case "3":
                    await SendBitcoin(manager);
                    break;
                case "4":
                    await PerformSwap(manager);
                    break;
                case "5":
                    await ShowTransactionHistory(manager);
                    break;
                case "6":
                    Console.WriteLine("Disconnecting...");
                    await manager.Disconnect();
                    Console.WriteLine("Goodbye!");
                    return;
                default:
                    Console.WriteLine("Invalid choice");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            Log.Error(ex, "Operation failed");
        }
    }
}

static async Task ShowBalance(BreezWalletManager manager)
{
    Console.WriteLine("\n=== Checking Balance ===");
    var balance = await manager.GetBalance();

    Console.WriteLine($"\n💰 Total Balance: {balance.TotalSats:N0} sats ({balance.TotalBtc:F8} BTC)");
    Console.WriteLine($"   Lightning: {balance.LightningSats:N0} sats");
    Console.WriteLine($"   On-chain: {balance.OnchainSats:N0} sats");
}

static async Task GetReceivingInfo(BreezWalletManager manager)
{
    Console.WriteLine("\n=== Receive Bitcoin ===");
    Console.Write("Enter amount in sats (or press Enter for any amount): ");
    var input = Console.ReadLine();

    ulong? amountSats = null;
    if (!string.IsNullOrWhiteSpace(input) && ulong.TryParse(input, out var amount))
    {
        amountSats = amount;
    }

    var invoice = await manager.CreateInvoice(amountSats, "Payment");

    Console.WriteLine("\n📥 Lightning Invoice:");
    Console.WriteLine("════════════════════════════════════════");
    Console.WriteLine(invoice);
    Console.WriteLine("════════════════════════════════════════");
    Console.WriteLine("\nShare this invoice to receive payment");
}

static async Task SendBitcoin(BreezWalletManager manager)
{
    Console.WriteLine("\n=== Send Bitcoin ===");
    Console.Write("Enter Lightning invoice or Bitcoin address: ");
    var destination = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(destination))
    {
        Console.WriteLine("Invalid destination");
        return;
    }

    Console.Write("Enter amount in sats (leave empty if invoice has amount): ");
    var amountInput = Console.ReadLine();

    ulong? amountSats = null;
    if (!string.IsNullOrWhiteSpace(amountInput) && ulong.TryParse(amountInput, out var amount))
    {
        amountSats = amount;
    }

    Console.WriteLine("\nProcessing payment...");
    var result = await manager.SendPayment(destination, amountSats);

    Console.WriteLine($"\n✅ Payment sent successfully!");
    Console.WriteLine($"   Payment Hash: {result.PaymentHash}");
    Console.WriteLine($"   Amount: {result.AmountSats:N0} sats");
    Console.WriteLine($"   Fee: {result.FeeSats:N0} sats");
}

static async Task PerformSwap(BreezWalletManager manager)
{
    Console.WriteLine("\n=== Swap Bitcoin ===");
    Console.WriteLine("1. On-chain → Lightning (Submarine Swap)");
    Console.WriteLine("2. Lightning → On-chain (Reverse Swap)");
    Console.Write("\nYour choice: ");

    var choice = Console.ReadLine();

    switch (choice)
    {
        case "1":
            await SwapToLightning(manager);
            break;
        case "2":
            await SwapToOnchain(manager);
            break;
        default:
            Console.WriteLine("Invalid choice");
            break;
    }
}

static async Task SwapToLightning(BreezWalletManager manager)
{
    Console.WriteLine("\n=== Swap On-chain → Lightning ===");

    var swapInfo = await manager.GetReceiveOnchainAddress();

    Console.WriteLine($"\n📬 Send Bitcoin to this address:");
    Console.WriteLine("════════════════════════════════════════");
    Console.WriteLine(swapInfo.Address);
    Console.WriteLine("════════════════════════════════════════");
    Console.WriteLine($"\nMin amount: {swapInfo.MinSats:N0} sats");
    Console.WriteLine($"Max amount: {swapInfo.MaxSats:N0} sats");
    Console.WriteLine($"Estimated fee: {swapInfo.FeeSats:N0} sats");
    Console.WriteLine("\nBitcoin will automatically appear in your Lightning balance");
}

static async Task SwapToOnchain(BreezWalletManager manager)
{
    Console.WriteLine("\n=== Swap Lightning → On-chain ===");
    Console.Write("Enter on-chain Bitcoin address: ");
    var address = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(address))
    {
        Console.WriteLine("Invalid address");
        return;
    }

    Console.Write("Enter amount in sats: ");
    if (!ulong.TryParse(Console.ReadLine(), out var amountSats))
    {
        Console.WriteLine("Invalid amount");
        return;
    }

    Console.WriteLine("\nProcessing reverse swap...");
    var result = await manager.SendToOnchain(address, amountSats);

    Console.WriteLine($"\n✅ Swap initiated!");
    Console.WriteLine($"   Transaction ID: {result.TxId}");
    Console.WriteLine($"   Amount: {result.AmountSats:N0} sats");
    Console.WriteLine($"   Fee: {result.FeeSats:N0} sats");
}

static async Task ShowTransactionHistory(BreezWalletManager manager)
{
    Console.WriteLine("\n=== Transaction History ===");
    var transactions = await manager.GetTransactions(limit: 20);

    if (transactions.Count == 0)
    {
        Console.WriteLine("No transactions yet");
        return;
    }

    foreach (var tx in transactions)
    {
        var direction = tx.IsIncoming ? "📥" : "📤";
        var type = tx.Type;
        Console.WriteLine($"\n{direction} {type}");
        Console.WriteLine($"   Amount: {tx.AmountSats:N0} sats");
        Console.WriteLine($"   Fee: {tx.FeeSats:N0} sats");
        Console.WriteLine($"   Date: {tx.Timestamp:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"   Status: {tx.Status}");
    }
}
