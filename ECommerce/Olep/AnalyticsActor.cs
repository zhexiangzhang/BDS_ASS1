using ECommerce.Olep.Interfaces;
using ECommerce.Olep.Schema;
using Orleans.Concurrency;
using Orleans.Streams;
using Utilities;

namespace ECommerce.Olep
{
    [Reentrant]
    public class AnalyticsActor : Grain, IAnalyticsActor
    {

        private readonly Dictionary<long, double> query;

        public AnalyticsActor()
        {
            this.query = new Dictionary<long, double>();
        }

        // just to force stream subscription
        public Task Init()
        {
            return Task.CompletedTask;
        }

        public override async Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var streamProvider = this.GetStreamProvider(Constants.DefaultStreamProvider);
            var stream = streamProvider.GetStream<Outcome>(Constants.OutcomeNamespace, "0");
            await stream.SubscribeAsync(UpdateAsync);
        }

        private Task UpdateAsync(Outcome outcome, StreamSequenceToken token = null)
        {
            // TODO implement this functionality
            throw new ApplicationException();
        }

        public async Task<List<KeyValuePair<long, double>>> Top10()
        {
            // TODO implement this functionality
            throw new ApplicationException();
        }

    }
}

