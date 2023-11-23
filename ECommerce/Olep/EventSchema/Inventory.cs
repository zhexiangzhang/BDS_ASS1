namespace ECommerce.Olep.Schema
{
    [Serializable]
    public sealed class Inventory
    {
        public readonly long productId;
        public readonly double price;
        public readonly int quantity;

        public Inventory(long productId, double price, int quantity)
        {
            this.productId = productId;
            this.price = price;
            this.quantity = quantity;
        }
    }
}

