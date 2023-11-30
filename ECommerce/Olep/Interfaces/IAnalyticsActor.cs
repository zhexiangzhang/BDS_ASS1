namespace ECommerce.Olep.Interfaces
{
    public interface IAnalyticsActor : IGrainWithGuidKey
    {
        // not supposed to be acessed by other actors, it is an API for clients
        Task Init();
        
        // accessed by transaction client
        Task Top10ToStream();
        Task<List<KeyValuePair<long, double>>> Top10();


    }
}

