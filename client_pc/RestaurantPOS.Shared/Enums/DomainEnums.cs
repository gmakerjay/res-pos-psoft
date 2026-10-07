namespace RestaurantPOS.Shared.Enums;

public enum OrderStatus
{
    New = 1,
    Accepted = 2,
    Preparing = 3,
    Ready = 4,
    Completed = 5,
    Cancelled = 6
}

public enum OrderType
{
    DineIn = 1,
    TakeAway = 2,
    Delivery = 3
}

public enum PaymentMethod
{
    Cash = 1,
    PromptPayQR = 2,
    CreditCard = 3,
    Transfer = 4
}

public enum TableStatus
{
    Available = 0,
    Occupied = 1,
    Reserved = 2,
    Billing = 3
}

public enum UserRole
{
    SuperAdmin = 1,
    Manager = 2,
    Cashier = 3,
    Waiter = 4,
    Kitchen = 5
}

public enum PosLogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    Fatal = 4
}
