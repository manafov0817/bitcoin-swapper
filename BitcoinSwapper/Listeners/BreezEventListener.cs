using Breez.Sdk;
using Serilog;

namespace BitcoinSwapper.Listeners;

/// <summary>
/// Event listener for Breez SDK events
/// </summary>
public class BreezEventListener : EventListener
{
    public void OnEvent(BreezEvent e)
    {
        Log.Information("Breez Event: {EventType}", e.GetType().Name);

        switch (e)
        {
            case BreezEvent.InvoicePaid invoicePaid:
                Log.Information("Invoice paid: {PaymentHash}", invoicePaid.details.payment.id);
                break;
            case BreezEvent.PaymentSucceed paymentSucceed:
                Log.Information("Payment succeeded: {PaymentHash}", paymentSucceed.details.id);
                break;
            case BreezEvent.PaymentFailed paymentFailed:
                Log.Error("Payment failed: {Error}", paymentFailed.details.error);
                break;
            case BreezEvent.Synced synced:
                Log.Information("Synced");
                break;
        }
    }
}
