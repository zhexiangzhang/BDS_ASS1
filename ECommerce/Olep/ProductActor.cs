using ECommerce.Olep.Interfaces;
using ECommerce.Olep.Schema;
using Orleans.Concurrency;
using Orleans.Streams;
using Utilities;

namespace ECommerce.Olep
{

    [Reentrant]
    public class ProductActor : Grain, IProductActor
    {

        private long id;
        private IStreamProvider streamProvider;
        private int quantity;
        private double price;

        public Task Init(double price, int quantity)
        {
            this.price = price;
            this.quantity = quantity;
            return Task.CompletedTask;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            this.id = this.GetPrimaryKeyLong();
            this.streamProvider = this.GetStreamProvider(Constants.DefaultStreamProvider);
            var streamIncoming = streamProvider.GetStream<Inventory>(Constants.InventoryNamespace, this.id.ToString());
            await streamIncoming.SubscribeAsync(ProcessInventoryRequest);
        }

        private async Task ProcessInventoryRequest(Inventory inventory, StreamSequenceToken token)
        {
            try
            {
                var ifEnoughQnt = inventory.quantity < quantity;
                if (ifEnoughQnt) quantity = quantity - inventory.quantity;
                else quantity = quantity + (inventory.quantity * 2) - inventory.quantity;
                var outStream = streamProvider.GetStream<Outcome>(Constants.OutcomeNamespace, "0");
                var outcome = new Outcome(inventory.productId, id, inventory.quantity * inventory.price, Status.OK);
                await outStream.OnNextAsync(outcome);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.ToString());
            }
        }

        public Task<double> GetPrice()
        {
            return Task.FromResult(this.price);
        }

        public Task<int> GetInventory()
        {
            return Task.FromResult(this.quantity);
        }
    }
}

