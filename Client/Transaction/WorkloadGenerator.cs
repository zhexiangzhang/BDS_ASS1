﻿using MathNet.Numerics.Distributions;
using ECommerce.Olep.Interfaces;
using Utilities;
using Orleans.Streams;
using ECommerce.Olep.Schema;
using System.Text;
using System.ComponentModel;

namespace Client.Transaction
{
    internal class WorkloadGenerator
    {
        readonly int numCustomerActor;
        readonly int numProductActor;
        IClusterClient client;
        private List<IAnalyticsActor> analyticsActors;
        private List<bool> analyticsActorsStatuses;
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

        public async Task InitAllActors(int numAnalyticsActor = 10)
        {
            // Initialize the grains and their statuses
            analyticsActors = new List<IAnalyticsActor>(numAnalyticsActor);
            analyticsActorsStatuses = new List<bool>(numAnalyticsActor);
            for (int i = 0; i < numAnalyticsActor; i++)
            {
                analyticsActors.Add(client.GetGrain<IAnalyticsActor>(i));
                analyticsActorsStatuses.Add(false);
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

        public async Task<string> GetTopTen()
        {
            StringBuilder sb = new StringBuilder();
            // Find the first grain that is not currently calculating the top 10
            for (int i = 0; i < analyticsActors.Count; i++)
            {
                if (!analyticsActorsStatuses[i])
                {
                    // Mark the grain as busy
                    analyticsActorsStatuses[i] = true;

                    // Ask the grain to calculate the top 10
                    List<KeyValuePair<long, double>> res = await client.GetGrain<IAnalyticsActor>(i).Top10();
                    foreach(KeyValuePair<long, double> kv in res)
                    {
                        sb.Append (kv.Key);
                        sb.Append(" : ");
                        sb.Append(kv.Value);
                        sb.AppendLine();
                    }
                    
                    // Mark the grain as free
                    analyticsActorsStatuses[i] = false;

                    return sb.ToString();
                }
            }
            // If all grains are busy, return an empty list / or just wait?
            
            sb.Append("All grains are busy!");
            return sb.ToString();

    
            
            // List<KeyValuePair<long, double>> res = await client.GetGrain<IAnalyticsActor>(0).Top10();
            // StringBuilder sb = new StringBuilder();
            // foreach(KeyValuePair<long, double> kv in res)
            // {
            //     sb.Append (kv.Key);
            //     sb.Append(" : ");
            //     sb.Append(kv.Value);
            //     sb.AppendLine();
            // }
            // return sb.ToString();
        }

    }
}