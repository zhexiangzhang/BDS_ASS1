﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace Client.Transaction
{
    internal class TransactionClient
    {
        int concurrencyLevel = 20;

        int numCustomerActor = 2000;
        int numProductActor = 100;

        // for experiment setting
        int numCustomerThread = 8;
        // int numGetTopTenThread = 8;

        TimeSpan runTime = TimeSpan.FromSeconds(10);    // use this time to control how long time the experiment will run
        TimeSpan topTenTaskRunTime = TimeSpan.FromSeconds(10);

        CountdownEvent allThreadsStart;
        CountdownEvent allThreadsAreDone;

        ConcurrentBag<Tuple<DateTime, DateTime>> topTenTaskLatency = new ConcurrentBag<Tuple<DateTime, DateTime>>();

        BlockingCollection<byte> resultQueue = new BlockingCollection<byte>();

        WorkloadGenerator workload;

        public async Task RunClient()
        {

            // ================================================================================================================
            // STEP 1: init all actors
            workload = new WorkloadGenerator(numCustomerActor, numProductActor);
            await workload.InitAllActors();
            Console.WriteLine("\n ***********************************************************************");
            Console.WriteLine($"#customer = {numCustomerActor}, #product = {numProductActor}");

            // ================================================================================================================
            // STEP 2: get initial inventory of all products
            var before_totalAmount = (await workload.GetAllInventory()).Item1.Sum();

            // ================================================================================================================
            // STEP 3: spawn multiple threads to submit transactions
            allThreadsStart = new CountdownEvent(numCustomerThread);
            allThreadsAreDone = new CountdownEvent(numCustomerThread);
            Console.WriteLine("\n ***********************************************************************");
            Console.WriteLine($"Spawning {numCustomerThread} threads to check-out order");
            for (int i = 0; i < numCustomerThread; i++)
            {
                var thread = new Thread(CustomerWorkAsync);
                thread.Start(i);
            }

            allThreadsAreDone.Wait();   // wait until all threads are done

            // ================================================================================================================
            // STEP 4: check inventory of all products again
            var res = await workload.GetAllInventory();
            var inventory = res.Item1;
            var hasEverGotNegativeInventory = res.Item2;
            Console.WriteLine("\n ***********************************************************************");
            if (hasEverGotNegativeInventory) Console.WriteLine($"The inventory has once become negative!!!");
            Console.WriteLine("\n ***********************************************************************");

            // the top-10 customers
            Console.WriteLine($"The top-10 customers are: ");
            var top10 = await workload.GetTopTen();
            Console.WriteLine(top10);
            Console.WriteLine("\n ***********************************************************************");





            // ================================================================================================================
            // STEP 5: start tasks to get top-10 customers to stress analytics actor            
            Console.WriteLine($"Concurrency level = {concurrencyLevel}");

            int submitCount = 0;
            var tasks = new List<Task>(concurrencyLevel);

            var cancellationToken = new CancellationTokenSource();

            Stopwatch s = new Stopwatch();
            s.Start();
            // get the time stamp of the start of the experiment
            DateTime startTime = DateTime.Now;
            while (submitCount < concurrencyLevel)
            {
                tasks.Add(Task.Run(() => GetTopTenAsync(cancellationToken.Token)));
                submitCount++;
            }
            while (s.Elapsed < topTenTaskRunTime)
            {
                tasks.Add(Task.Run(() => GetTopTenAsync(cancellationToken.Token)));
                submitCount++;
                while (resultQueue.TryTake(out _) && s.Elapsed < topTenTaskRunTime) { }
            }
            DateTime endTime = startTime.Add(topTenTaskRunTime);


            // clean up the remaining tasks
            cancellationToken.Cancel();

            // calculate the average end to end latency and throughput of GetTopTen() from topTenTaskLatency            
            var averageExecutionTime = TimeSpan.Zero;
            int finishedTaskCount = 0;
            foreach (var tuple in topTenTaskLatency)
            {
                // only conut if the finish time of the task is before the end time of the experiment
                if (tuple.Item2 < endTime)
                {
                    averageExecutionTime += (tuple.Item2 - tuple.Item1);
                    finishedTaskCount++;
                }
            }

            averageExecutionTime = TimeSpan.FromMilliseconds(averageExecutionTime.TotalMilliseconds / finishedTaskCount);
            var throughput = finishedTaskCount / topTenTaskRunTime.TotalSeconds;

            double averageExecutionTimeInMilliseconds = averageExecutionTime.TotalMilliseconds;

            Console.WriteLine("\n ***********************************************************************");
            Console.WriteLine($"Concurrency level = {concurrencyLevel} Average execution time = {averageExecutionTimeInMilliseconds} ms Throughput = {throughput}");
            Console.WriteLine("\n ***********************************************************************");

            Console.WriteLine("\n\nThe experiment is done. ");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        // ================================================================================================================
        async void CustomerWorkAsync(object obj)
        {
            var thread = (int)obj;
            var numEmitTransaction = 0;
            //var workload = new WorkloadGenerator(numCustomerActor, numProductActor);
            var watch = new Stopwatch();

            allThreadsStart.Signal();
            allThreadsStart.Wait();      // make sure all threads start at the same time

            watch.Start();
            while (watch.Elapsed < runTime)
            {
                numEmitTransaction++;
                await workload.NewCheckOutOrder();   // submit one transaction a time
            }
            var totalTime = watch.Elapsed.TotalMilliseconds;

            Console.WriteLine($"Thread {thread}: " +
                              $"Number of transactions emitted = {numEmitTransaction} " +
                              $"Total time elapsed = {totalTime}");
            allThreadsAreDone.Signal();
        }

        async void GetTopTenAsync(CancellationToken cancellationToken)
        {
            // var workload = new WorkloadGenerator(numCustomerActor, numProductActor);

            DateTime start = DateTime.Now;
            if (cancellationToken.IsCancellationRequested) return;
            await workload.GetTopTen();
            if (cancellationToken.IsCancellationRequested) return;
            DateTime end = DateTime.Now;
            this.resultQueue.Add(1);
            this.topTenTaskLatency.Add(new Tuple<DateTime, DateTime>(start, end));
        }

    }
}

// dotnet run --project Server
// Concurrency level = 1 Average execution time = 771.9999 ms Throughput = 2232.3
// Concurrency level = 5 Average execution time = 834.5639 ms Throughput = 2000.4
// Concurrency level = 10 Average execution time = 934.9092 ms Throughput = 2057.2
// Concurrency level = 20 Average execution time = 784.5506 ms Throughput = 2135.1

// Concurrency level = 1 Average execution time = 2034.9743 ms Throughput = 14.5
// Concurrency level = 5 Average execution time = 1524.58 ms Throughput = 13.7
// Concurrency level = 10 Average execution time = 1609.6113 ms Throughput = 16.7

// Concurrency level = 20 Average execution time = 4951.2936 ms Throughput = 1.5
// Concurrency level = 20 Average execution time = 178.3134 ms Throughput = 99.1 
// Concurrency level = 20 Average execution time = 372.837 ms Throughput = 2794.2 (main)
// Concurrency level = 20 Average execution time = 5.7549 ms Throughput = 3908.1 (random1)
// Concurrency level = 20 Average execution time = 2.8297 ms Throughput = 5166.8 (random10)
