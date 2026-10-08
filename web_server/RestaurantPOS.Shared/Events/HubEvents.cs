namespace RestaurantPOS.Shared.Events;

public static class HubEvents
{
    // Hub route
    public const string HubUrl = "/hubs/pos";

    // Server-to-Client methods
    public const string OrderCreated = "OrderCreated";
    public const string OrderStatusChanged = "OrderStatusChanged";
    public const string OrderItemAdded = "OrderItemAdded";
    public const string TableStatusChanged = "TableStatusChanged";
    public const string BillClosed = "BillClosed";
    public const string MenuUpdated = "MenuUpdated";
    public const string StockChanged = "StockChanged";
    public const string IngredientUpdated = "IngredientUpdated";
    public const string CategoryUpdated = "CategoryUpdated";
    public const string SystemNotification = "SystemNotification";
    public const string StoreStatusChanged = "StoreStatusChanged";
    public const string OrderActionActivity = "OrderActionActivity";

    // Client-to-Server methods
    public const string JoinTableGroup = "JoinTableGroup";
    public const string LeaveTableGroup = "LeaveTableGroup";
    public const string JoinRoleGroup = "JoinRoleGroup";
    public const string RegisterPos = "RegisterPos";
    public const string UnregisterPos = "UnregisterPos";
    public const string NotifyOrderAction = "NotifyOrderAction";
}
