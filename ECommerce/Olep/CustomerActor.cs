using ECommerce.Olep.Interfaces;
using ECommerce.Olep.Schema;
using Orleans.Concurrency;
using Orleans.Streams;
using Utilities;

namespace ECommerce.Olep
{

    [Reentrant]
    public class CustomerActor : Grain, ICustomerActor
    {
        private long id;
        private double balance;

        private IStreamProvider streamProvider;

        public Task Init(double balance)
        {
            this.balance = balance;
            return Task.CompletedTask;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            this.id = this.GetPrimaryKeyLong();
            this.streamProvider = this.GetStreamProvider(Constants.DefaultStreamProvider);
            var streamIncoming = streamProvider.GetStream<Checkout>(Constants.CheckoutNamespace, this.id.ToString());
            await streamIncoming.SubscribeAsync(ProcessCheckout);

        }

        private async Task ProcessCheckout(Checkout checkout, StreamSequenceToken token = null)
        {
            try
            {
                var ifEnoughBalance = checkout.price * checkout.quantity < balance;
                if (ifEnoughBalance) 
                {   
                    IAsyncStream<Inventory> productStream = streamProvider.GetStream<Inventory>(Constants.InventoryNamespace, checkout.productId.ToString());
                    await productStream.OnNextAsync(new Inventory(id, checkout.price, checkout.quantity));
                }
                else { 
                    var outStream = streamProvider.GetStream<Outcome>(Constants.OutcomeNamespace, 0);
                    var outcome = new Outcome(id, checkout.productId, checkout.quantity * checkout.price, Status.INSUFFICIENT_BALANCE);
                    await outStream.OnNextAsync(outcome);
                }
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.ToString());
            }
            
            
        }
      
    }
}

