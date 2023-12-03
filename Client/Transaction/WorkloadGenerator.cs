using MathNet.Numerics.Distributions;
using ECommerce.Olep.Interfaces;
using Utilities;
using Orleans.Streams;
using ECommerce.Olep.Schema;
using System.Text;
using System.ComponentModel;
using System.Collections.Concurrent;
using ECommerce.Olep;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace Client.Transaction
{
    internal class WorkloadGenerator
    {

        readonly int numAnalyticsActor = 1;

        readonly int numCustomerActor;
        readonly int numProductActor;
        IClusterClient client;
        private List<IAnalyticsActor> analyticsActors;
                                
        bool isClientConnected = false;        

        IDiscreteDistribution customerDistribution;       // which customer send the request
        IDiscreteDistribution productDistribution;        // which product to buy
        IDiscreteDistribution productQtyDistribution;     // the number of items available for each product
        IDiscreteDistribution productPriceDistribution;   // the price of items
        IDiscreteDistribution customerBalanceDistribution;// the customer balance
        IDiscreteDistribution customerQtyDistribution;    // max qty a customer can buy for a product

        public WorkloadGenerator(int numCustomerActor, int numProductActor)
        {
         
            this.numCustomerActor = numCustomerActor;
            this.numProductActor = numProductActor;
            // it will generate samples within range [a, b]
            customerDistribution = new DiscreteUniform(0, numCustomerActor - 1, new Random());
            productDistribution = new DiscreteUniform(0, numProductActor - 1, new Random());
            productQtyDistribution = new DiscreteUniform(1, 100, new Random());
            productPriceDistribution = new DiscreteUniform(1, 1000, new Random());
            customerBalanceDistribution = new DiscreteUniform(1, 10000, new Random());
            customerQtyDistribution = new DiscreteUniform(1, 10, new Random());                    

            // wait until the client is created and connected
            InitiateClient();
            while (isClientConnected == false) Thread.Sleep(TimeSpan.FromMilliseconds(100));
        }

        async void InitiateClient()
        {
            client = await OrleansClientManager.GetClient();
            isClientConnected = true;
        }

        public async Task InitAllActors()
        {
            // Initialize the grains and their statuses
            analyticsActors = new List<IAnalyticsActor>(numAnalyticsActor);                                       

            for (int i = 0; i < numAnalyticsActor; i++)
            {
                analyticsActors.Add(client.GetGrain<IAnalyticsActor>(i));                                
                await analyticsActors[i].Init();
            }
            // var analyticsActor = client.GetGrain<IAnalyticsActor>(0);
            // await analyticsActor.Init();

            var tasks = new List<Task>();
            for (int i = 0; i < numCustomerActor; i++)
            {
                var customerActor = client.GetGrain<ICustomerActor>(i);
                tasks.Add(customerActor.Init(customerBalanceDistribution.Sample()));  
            }

            for (int i = 0; i < numProductActor; i++)
            {
                var productActor = client.GetGrain<IProductActor>(i);
                tasks.Add(productActor.Init(productPriceDistribution.Sample(), productQtyDistribution.Sample()));
            }
               
            await Task.WhenAll(tasks);
        }

        public async Task<Tuple<List<long>, bool>> GetAllInventory()
        {
            var tasks = new List<Task<int>>();
            for (int i = 0; i < numProductActor; i++)
            {
                var productActor = client.GetGrain<IProductActor>(i);
                tasks.Add(productActor.GetInventory());
           
            }
            await Task.WhenAll(tasks);

            var hasEverGotNegativeInventory = false;
            var inventory = new List<long>();
            foreach (var task in tasks)
            {
                inventory.Add(task.Result);
                if (task.Result < 0) hasEverGotNegativeInventory = true;
            }
            return new Tuple<List<long>, bool>(inventory, hasEverGotNegativeInventory);
        }

        public async Task NewCheckOutOrder()
        {
   
            var customerID = customerDistribution.Sample();
            var productID = productDistribution.Sample();
            var qty = customerQtyDistribution.Sample();

            var price = await client.GetGrain<IProductActor>(productID).GetPrice();

            IStreamProvider streamProvider = client.GetStreamProvider(Constants.DefaultStreamProvider);

            IAsyncStream<Checkout> checkoutStream = streamProvider.GetStream<Checkout>( Constants.CheckoutNamespace, customerID.ToString() );
            await checkoutStream.OnNextAsync(new Checkout(customerID, price, qty));

            return;
        }

        public async Task<string> GetTopTen2(int transactionId)
        {
            List<KeyValuePair<long, double>> res = await client.GetGrain<IAnalyticsActor>(0).Top10();
            
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<long, double> kv in res)
            {
                sb.Append(kv.Key);
                sb.Append(" : ");
                sb.Append(kv.Value);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public async Task<string> GetTopTen(int transactionId)
        {
            List<KeyValuePair<long, double>> res = await client.GetGrain<IAnalyticsActor>(transactionId % numAnalyticsActor).Top10();            
            
            StringBuilder sb = new StringBuilder();                          
            foreach (KeyValuePair<long, double> kv in res)
            {
                sb.Append(kv.Key);
                sb.Append(" : ");
                sb.Append(kv.Value);
                sb.AppendLine();
            }
            return sb.ToString();
        }

    }
}


// Concurrency level = 1 Average execution time = 22.4743 ms Throughput = 205.2
// Concurrency level = 2 Average execution time = 37.7091 ms Throughput = 464.2
// Concurrency level = 2 Average execution time = 52.2408 ms Throughput = 638.6
// Concurrency level = 4 Average execution time = 63.2148 ms Throughput = 1574.1
// Concurrency level = 4 Average execution time = 158.9546 ms Throughput = 1019.8
// Concurrency level = 4 Average execution time = 55.8784 ms Throughput = 132.9
// Concurrency level = 4 Average execution time = 36.0386 ms Throughput = 1452.8
// Concurrency level = 8 Average execution time = 160.4237 ms Throughput = 2372.5
// Concurrency level = 16 Average execution time = 52.7176 ms Throughput = 494.6
// Concurrency level = 16 Average execution time = 19.7929 ms Throughput = 229.9
// Concurrency level = 16 Average execution time = 35.0911 ms Throughput = 299.3

// Concurrency level = 1 Average execution time = 1347.0049 ms Throughput = 48094.3
// Concurrency level = 2 Average execution time = 94.1894 ms Throughput = 121225.8
// Concurrency level = 2 Average execution time = 2672.1471 ms Throughput = 73564.7
// Concurrency level = 4 Average execution time = 3405.8463 ms Throughput = 45924.4
// Concurrency level = 4 Average execution time = 185.2396 ms Throughput = 110130.5
// Concurrency level = 4 Average execution time = 1929.4897 ms Throughput = 87537.7
// Concurrency level = 8 Average execution time = 684.2516 ms Throughput = 101363.8
// Concurrency level = 8 Average execution time = 67.7407 ms Throughput = 103218.3
// Concurrency level = 16 Average execution time = 486.1393 ms Throughput = 102023
// Concurrency level = 16 Average execution time = 88.9747 ms Throughput = 102769.2
