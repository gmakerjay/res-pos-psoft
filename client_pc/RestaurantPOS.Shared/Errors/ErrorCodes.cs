namespace RestaurantPOS.Shared.Errors;

public static class ErrorCodes
{
    // General
    public const string ServerError = "ERR_SERVER_ERROR";
    public const string ValidationError = "ERR_VALIDATION_FAILED";
    public const string NotFound = "ERR_NOT_FOUND";
    public const string Unauthorized = "ERR_UNAUTHORIZED";
    public const string Forbidden = "ERR_FORBIDDEN";
    public const string ConnectionFailed = "ERR_CONNECTION_FAILED";
    public const string Timeout = "ERR_TIMEOUT";
    public const string Conflict = "ERR_CONFLICT";

    // Orders & Billing
    public const string OrderNotFound = "ERR_ORDER_NOT_FOUND";
    public const string OrderAlreadyCompleted = "ERR_ORDER_ALREADY_COMPLETED";
    public const string OrderAlreadyCancelled = "ERR_ORDER_ALREADY_CANCELLED";
    public const string InvalidOrderStatusTransition = "ERR_INVALID_STATUS_TRANSITION";
    public const string TableNotFound = "ERR_TABLE_NOT_FOUND";
    public const string TableOccupied = "ERR_TABLE_OCCUPIED";
    public const string EmptyOrder = "ERR_EMPTY_ORDER";
    public const string DuplicateOrder = "ERR_DUPLICATE_ORDER";
    public const string StoreOffline = "ERR_STORE_OFFLINE";

    // Products & Stock
    public const string ProductNotFound = "ERR_PRODUCT_NOT_FOUND";
    public const string InsufficientStock = "ERR_INSUFFICIENT_STOCK";
    public const string CategoryNotFound = "ERR_CATEGORY_NOT_FOUND";

    // Hardware & Printing
    public const string PrinterOffline = "ERR_PRINTER_OFFLINE";
    public const string PrinterPaperOut = "ERR_PRINTER_PAPER_OUT";
    public const string PrinterSpoolerError = "ERR_PRINTER_SPOOLER_ERROR";

    // Auth & Permission
    public const string InvalidCredentials = "ERR_INVALID_CREDENTIALS";
    public const string UserDisabled = "ERR_USER_DISABLED";
    public const string PermissionDenied = "ERR_PERMISSION_DENIED";
}
