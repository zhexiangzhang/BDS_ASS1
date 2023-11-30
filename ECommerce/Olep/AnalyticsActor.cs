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
        private IStreamProvider streamProvider;

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

        // public async Task Top10()
        // {   
        //     try 
        //     {
        //         var top10 = query.OrderByDescending(pair => pair.Value)
        //                 .Take(10)
        //                 .ToList();
        
        //         client = await OrleansClientManager.GetClient();
        //         async void InitiateClient()
        

        //         // Get a reference to another AnalyticsActor
        //         var newTop10Actor = client.GetGrain<IAnalyticsActor>(Guid.NewGuid());

        //         // Start the other actor's Top10ToStream method without awaiting it
        //         Task.Run(() => newTop10Actor.Top10ToStream(query));
        //     }
        //     catch (Exception e) 
        //     {
        //         throw new ApplicationException(e.ToString());
        //     }
        // }

        public async Task Top10()
        {
            try 
            {
                // Get a reference to another AnalyticsActor
                var newAnalyticsActor = GrainFactory.GetGrain<IAnalyticsActor>(Guid.NewGuid());

                // Initiate the new actor
                await newAnalyticsActor.Init();

                // Start the other actor's Top10ToStream method without awaiting it
                Task.Run(() => newAnalyticsActor.Top10ToStream());
            }
            catch (Exception e) 
            {
                throw new ApplicationException(e.ToString());
            }
        }

        public async Task Top10ToStream()
        {
            var top10 = query.OrderByDescending(pair => pair.Value)
                        .Take(10)
                        .ToList();

            // Get a reference to the stream
            var streamProvider = GetStreamProvider(Constants.DefaultStreamProvider);
            var stream = streamProvider.GetStream<List<KeyValuePair<long, double>>>(this.GetPrimaryKey(), "Top10");

            // Output the top10 data to the stream
            await stream.OnNextAsync(top10);
        }

    }
}

