namespace Dmd30CustomerDisplay;

public sealed record ReceiptData(long SaleId, DateTime SoldAt, string StoreName, IReadOnlyList<ReceiptLine> Lines, int TotalQuantity, int TotalAmount, int CashAmount, int CardAmount, int OtherAmount, int ChangeAmount);

public sealed record ReceiptLine(string Name, int Quantity, int UnitPrice, int Amount);
