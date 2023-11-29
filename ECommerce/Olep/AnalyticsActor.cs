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
            try
            {
                if (outcome.status == Status.INSUFFICIENT_BALANCE) return Task.CompletedTask;
                var keys = query.Keys;
                if (keys.Contains(outcome.customerId))
                {
                    var key = outcome.customerId;
                    var spend = query[key];
                    query[key] = spend + outcome.total;
                    return Task.CompletedTask;
                }
                query.Add(outcome.customerId, outcome.total);
                return Task.CompletedTask;
            }
            catch (Exception e) 
            {
                throw new ApplicationException(e.ToString());
            }
        }

        public async Task<List<KeyValuePair<long, double>>> Top10()
        {
            try 
            {
                // List<KeyValuePair<long, double>> top10 = new List<KeyValuePair<long, double>>();
                // TODO: Add logic to populate the 'top10' list

                var top10 = await Task.Run(() =>
                {
                    return query.OrderByDescending(pair => pair.Value)
                                .Take(10)
                                .ToList();
                });

                return top10;
            }
            catch (Exception e) 
            {
                throw new ApplicationException(e.ToString());
            }
        }

    }
}

