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
            // TODO implement this functionality
            throw new ApplicationException();
        }
      
    }
}

