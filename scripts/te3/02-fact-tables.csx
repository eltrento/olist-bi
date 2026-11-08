// Group 2: fact tables (Order Items, Payments, Reviews).
// Same pattern as group 1: CSVs read from DataPath, columns named in M, SourceColumn = model name.
// Keys are hidden and SummarizeBy is set in group 4.

const string Csv = "[Delimiter=\",\", Encoding=65001, QuoteStyle=QuoteStyle.Csv]";
Func<string, string> Read = file =>
    "Table.PromoteHeaders(Csv.Document(File.Contents(DataPath & \"" + file + "\"), " + Csv + "), [PromoteAllScalars=true])";

Action<Table, string, string, DataType, string> Col = (t, name, folder, type, fmt) => {
    var c = t.AddDataColumn(name, name, folder, type);
    if (fmt != null) c.FormatString = fmt;
};

// --- Order Items ---------------------------------------------------------------
var items = Model.AddTable("Order Items", false);
items.Description = "Fact. One row per item in an order (Order ID + Order Item Number).";
items.AddMPartition("Order Items", @"let
    Source = " + Read("olist_order_items_dataset.csv") + @",
    Typed = Table.TransformColumnTypes(Source, {
        {""order_id"", type text}, {""order_item_id"", Int64.Type}, {""product_id"", type text}, {""seller_id"", type text},
        {""shipping_limit_date"", type datetime}, {""price"", Currency.Type}, {""freight_value"", Currency.Type}}, ""en-US""),
    Result = Table.RenameColumns(Typed, {
        {""order_id"", ""Order ID""}, {""order_item_id"", ""Order Item Number""}, {""product_id"", ""Product ID""},
        {""seller_id"", ""Seller ID""}, {""shipping_limit_date"", ""Shipping Limit Timestamp""},
        {""price"", ""Price""}, {""freight_value"", ""Freight Value""}})
in
    Result");
Col(items, "Order ID", null, DataType.String, null);
Col(items, "Order Item Number", null, DataType.Int64, "0");
Col(items, "Product ID", null, DataType.String, null);
Col(items, "Seller ID", null, DataType.String, null);
Col(items, "Shipping Limit Timestamp", null, DataType.DateTime, "yyyy-mm-dd hh:nn:ss");
Col(items, "Price", null, DataType.Decimal, "#,0.00");
Col(items, "Freight Value", null, DataType.Decimal, "#,0.00");
items.Columns["Price"].Description = "Item price in BRL, excluding freight.";
items.Columns["Freight Value"].Description = "Freight charged for the item in BRL.";
items.Columns["Shipping Limit Timestamp"].Description = "Seller's shipping deadline. Not related to Date (a few values fall in 2020).";

// --- Payments ------------------------------------------------------------------
var payments = Model.AddTable("Payments", false);
payments.Description = "Fact. One row per payment of an order (Order ID + Payment Sequential).";
payments.AddMPartition("Payments", @"let
    Source = " + Read("olist_order_payments_dataset.csv") + @",
    Typed = Table.TransformColumnTypes(Source, {
        {""order_id"", type text}, {""payment_sequential"", Int64.Type}, {""payment_type"", type text},
        {""payment_installments"", Int64.Type}, {""payment_value"", Currency.Type}}, ""en-US""),
    ProperType = Table.TransformColumns(Typed, {{""payment_type"", each Text.Proper(Text.Replace(_, ""_"", "" "")), type text}}),
    Result = Table.RenameColumns(ProperType, {
        {""order_id"", ""Order ID""}, {""payment_sequential"", ""Payment Sequential""}, {""payment_type"", ""Payment Type""},
        {""payment_installments"", ""Payment Installments""}, {""payment_value"", ""Payment Value""}})
in
    Result");
Col(payments, "Order ID", null, DataType.String, null);
Col(payments, "Payment Sequential", null, DataType.Int64, "0");
Col(payments, "Payment Type", null, DataType.String, null);
Col(payments, "Payment Installments", null, DataType.Int64, "0");
Col(payments, "Payment Value", null, DataType.Decimal, "#,0.00");
payments.Columns["Payment Type"].Description = "Credit Card, Boleto, Voucher, Debit Card or Not Defined.";
payments.Columns["Payment Value"].Description = "Amount paid in BRL (includes freight; vouchers and installments make it differ from item totals).";

// --- Reviews -------------------------------------------------------------------
var reviews = Model.AddTable("Reviews", false);
reviews.Description = "Fact. One row per review of an order (Review ID + Order ID). Comment text is not loaded.";
reviews.AddMPartition("Reviews", @"let
    Source = " + Read("olist_order_reviews_dataset.csv") + @",
    Typed = Table.TransformColumnTypes(Source, {
        {""review_id"", type text}, {""order_id"", type text}, {""review_score"", Int64.Type},
        {""review_creation_date"", type datetime}, {""review_answer_timestamp"", type datetime}}, ""en-US""),
    AddHasComment = Table.AddColumn(Typed, ""Has Comment"", each
        [review_comment_message] <> null and Text.Trim([review_comment_message]) <> """", type logical),
    AddCreatedDate = Table.AddColumn(AddHasComment, ""Review Created Date"", each DateTime.Date([review_creation_date]), type date),
    Renamed = Table.RenameColumns(AddCreatedDate, {
        {""review_id"", ""Review ID""}, {""order_id"", ""Order ID""}, {""review_score"", ""Review Score""},
        {""review_answer_timestamp"", ""Review Answer Timestamp""}}),
    Result = Table.SelectColumns(Renamed, {
        ""Review ID"", ""Order ID"", ""Review Score"", ""Review Created Date"", ""Review Answer Timestamp"", ""Has Comment""})
in
    Result");
Col(reviews, "Review ID", null, DataType.String, null);
Col(reviews, "Order ID", null, DataType.String, null);
Col(reviews, "Review Score", null, DataType.Int64, "0");
Col(reviews, "Review Created Date", null, DataType.DateTime, "yyyy-mm-dd");
Col(reviews, "Review Answer Timestamp", null, DataType.DateTime, "yyyy-mm-dd hh:nn:ss");
Col(reviews, "Has Comment", null, DataType.Boolean, null);
reviews.Columns["Review Score"].Description = "Score from 1 to 5.";
reviews.Columns["Has Comment"].Description = "True when the review has a non-empty comment message.";

Output("Created Order Items (7 cols), Payments (5), Reviews (6).");
