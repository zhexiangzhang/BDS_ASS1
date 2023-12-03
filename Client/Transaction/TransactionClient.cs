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
                while (!resultQueue.TryTake(out _) && s.Elapsed < topTenTaskRunTime) { }
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

        // Concurrency level = 1 Average execution time = 0.4896 ms Throughput = 3068.7
        // Concurrency level = 2 Average execution time = 0.5142 ms Throughput = 3876.9
        // Concurrency level = 4 Average execution time = 0.5461 ms Throughput = 5037.4
        // Concurrency level = 8 Average execution time = 0.8307 ms Throughput = 5728.5
        // Concurrency level = 16 Average execution time = 1.1999 ms Throughput = 5854.3
        // Concurrency level = 32 Average execution time = 1.7799 ms Throughput = 5495.7
        // Concurrency level = 64 Average execution time = 1.3145 ms Throughput = 5906.4
        // Concurrency level = 128 Average execution time = 1.3057 ms Throughput = 5952.1

        // Concurrency level = 1 Average execution time = 0.2431 ms Throughput = 4056.6
        // Concurrency level = 2 Average execution time = 0.2247 ms Throughput = 5343.4
        // Concurrency level = 4 Average execution time = 0.2318 ms Throughput = 6446.4
        // Concurrency level = 8 Average execution time = 0.3317 ms Throughput = 6870.2
        // Concurrency level = 16 Average execution time = 0.4681 ms Throughput = 7047.3
        // Concurrency level = 32 Average execution time = 0.6358 ms Throughput = 7030.9

