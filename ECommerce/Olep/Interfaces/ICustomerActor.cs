namespace ECommerce.Olep.Interfaces
{
    public interface ICustomerActor : IGrainWithIntegerKey
    {
        // not supposed to be acessed by other actors, it is an API for clients
        Task Init(double balance);
    }
}

