// Group 1: DataPath parameter + dimension tables (Orders, Customer, Product, Seller).
// M partitions read the CSVs from DataPath; columns are named in M, so SourceColumn = model name.
// Keys are hidden and SummarizeBy is set in group 4.

const string Csv = "[Delimiter=\",\", Encoding=65001, QuoteStyle=QuoteStyle.Csv]";
Func<string, string> Read = file =>
    "Table.PromoteHeaders(Csv.Document(File.Contents(DataPath & \"" + file + "\"), " + Csv + "), [PromoteAllScalars=true])";

// --- DataPath parameter -------------------------------------------------------
var p = Model.AddExpression("DataPath",
    "\"C:\\Users\\trent\\Desktop\\olist-bi\\data\\raw\\archive\\\" meta [IsParameterQuery=true, Type=\"Text\", IsParameterQueryRequired=true]");
p.Kind = ExpressionKind.M;
p.Description = "Folder that contains the Olist CSVs (with trailing backslash).";

Action<Table, string, string, DataType, string> Col = (t, name, folder, type, fmt) => {
    var c = t.AddDataColumn(name, name, folder, type);
    if (fmt != null) c.FormatString = fmt;
};

// --- Orders --------------------------------------------------------------------
var orders = Model.AddTable("Orders", false);
orders.Description = "One row per order. Hub dimension: filters Order Items, Payments and Reviews.";
orders.AddMPartition("Orders", @"let
    Source = " + Read("olist_orders_dataset.csv") + @",
    Typed = Table.TransformColumnTypes(Source, {
        {""order_id"", type text}, {""customer_id"", type text}, {""order_status"", type text},
        {""order_purchase_timestamp"", type datetime}, {""order_approved_at"", type datetime},
        {""order_delivered_carrier_date"", type datetime}, {""order_delivered_customer_date"", type datetime},
        {""order_estimated_delivery_date"", type datetime}}, ""en-US""),
    Customers = Table.SelectColumns(" + Read("olist_customers_dataset.csv") + @", {""customer_id"", ""customer_unique_id""}),
    Merged = Table.NestedJoin(Typed, {""customer_id""}, Customers, {""customer_id""}, ""Cust"", JoinKind.LeftOuter),
    Expanded = Table.ExpandTableColumn(Merged, ""Cust"", {""customer_unique_id""}),
    AddPurchaseDate = Table.AddColumn(Expanded, ""Purchase Date"", each DateTime.Date([order_purchase_timestamp]), type date),
    AddDeliveredDate = Table.AddColumn(AddPurchaseDate, ""Delivered Date"", each DateTime.Date([order_delivered_customer_date]), type nullable date),
    AddEstimatedDate = Table.AddColumn(AddDeliveredDate, ""Estimated Delivery Date"", each DateTime.Date([order_estimated_delivery_date]), type date),
    AddIsSales = Table.AddColumn(AddEstimatedDate, ""Is Sales Order"", each not List.Contains({""canceled"", ""unavailable""}, [order_status]), type logical),
    AddIsDelivered = Table.AddColumn(AddIsSales, ""Is Delivered"", each [Delivered Date] <> null, type logical),
    AddIsLate = Table.AddColumn(AddIsDelivered, ""Is Late"", each if [Delivered Date] = null then null else [Delivered Date] > [Estimated Delivery Date], type nullable logical),
    AddDeliveryDays = Table.AddColumn(AddIsLate, ""Delivery Days"", each if [Delivered Date] = null then null else Duration.Days([Delivered Date] - [Purchase Date]), Int64.Type),
    ProperStatus = Table.TransformColumns(AddDeliveryDays, {{""order_status"", Text.Proper, type text}}),
    Renamed = Table.RenameColumns(ProperStatus, {
        {""order_id"", ""Order ID""}, {""customer_unique_id"", ""Customer Unique ID""}, {""order_status"", ""Order Status""},
        {""order_purchase_timestamp"", ""Purchase Timestamp""}, {""order_approved_at"", ""Approved Timestamp""},
        {""order_delivered_carrier_date"", ""Carrier Handoff Timestamp""}, {""order_delivered_customer_date"", ""Delivered Timestamp""}}),
    Result = Table.SelectColumns(Renamed, {
        ""Order ID"", ""Customer Unique ID"", ""Order Status"",
        ""Purchase Timestamp"", ""Purchase Date"", ""Approved Timestamp"", ""Carrier Handoff Timestamp"",
        ""Delivered Timestamp"", ""Delivered Date"", ""Estimated Delivery Date"",
        ""Is Sales Order"", ""Is Delivered"", ""Is Late"", ""Delivery Days""})
in
    Result");
Col(orders, "Order ID", null, DataType.String, null);
Col(orders, "Customer Unique ID", null, DataType.String, null);
Col(orders, "Order Status", null, DataType.String, null);
Col(orders, "Purchase Timestamp", "Timestamps", DataType.DateTime, "yyyy-mm-dd hh:nn:ss");
Col(orders, "Purchase Date", "Dates", DataType.DateTime, "yyyy-mm-dd");
Col(orders, "Approved Timestamp", "Timestamps", DataType.DateTime, "yyyy-mm-dd hh:nn:ss");
Col(orders, "Carrier Handoff Timestamp", "Timestamps", DataType.DateTime, "yyyy-mm-dd hh:nn:ss");
Col(orders, "Delivered Timestamp", "Timestamps", DataType.DateTime, "yyyy-mm-dd hh:nn:ss");
Col(orders, "Delivered Date", "Dates", DataType.DateTime, "yyyy-mm-dd");
Col(orders, "Estimated Delivery Date", "Dates", DataType.DateTime, "yyyy-mm-dd");
Col(orders, "Is Sales Order", "Flags", DataType.Boolean, null);
Col(orders, "Is Delivered", "Flags", DataType.Boolean, null);
Col(orders, "Is Late", "Flags", DataType.Boolean, null);
Col(orders, "Delivery Days", null, DataType.Int64, "0");
orders.Columns["Is Sales Order"].Description = "True unless the order status is Canceled or Unavailable.";
orders.Columns["Is Delivered"].Description = "True when the order has a delivered customer date (regardless of status).";
orders.Columns["Is Late"].Description = "Delivered Date > Estimated Delivery Date (date-only). Blank when not delivered.";
orders.Columns["Delivery Days"].Description = "Delivered Date - Purchase Date in days. Blank when not delivered.";

// --- Customer ------------------------------------------------------------------
var customer = Model.AddTable("Customer", false);
customer.Description = "One row per person (customer_unique_id). Location from the customer's most recent order.";
customer.AddMPartition("Customer", @"let
    Customers = " + Read("olist_customers_dataset.csv") + @",
    Orders = Table.TransformColumnTypes(
        Table.SelectColumns(" + Read("olist_orders_dataset.csv") + @", {""customer_id"", ""order_purchase_timestamp""}),
        {{""order_purchase_timestamp"", type datetime}}, ""en-US""),
    Merged = Table.NestedJoin(Customers, {""customer_id""}, Orders, {""customer_id""}, ""Ord"", JoinKind.LeftOuter),
    Expanded = Table.ExpandTableColumn(Merged, ""Ord"", {""order_purchase_timestamp""}),
    Grouped = Table.Group(Expanded, {""customer_unique_id""}, {{""Latest"", each Table.Max(_, ""order_purchase_timestamp""), type record}}),
    Latest = Table.ExpandRecordColumn(Grouped, ""Latest"", {""customer_zip_code_prefix"", ""customer_city"", ""customer_state""}),
    Cleaned = Table.TransformColumns(Latest, {
        {""customer_zip_code_prefix"", each Text.PadStart(_, 5, ""0""), type text},
        {""customer_city"", Text.Proper, type text},
        {""customer_state"", Text.Upper, type text}}),
    Renamed = Table.RenameColumns(Cleaned, {
        {""customer_unique_id"", ""Customer Unique ID""}, {""customer_zip_code_prefix"", ""Customer Zip Prefix""},
        {""customer_city"", ""Customer City""}, {""customer_state"", ""Customer State""}}),
    Result = Table.TransformColumnTypes(Renamed, {{""Customer Unique ID"", type text}})
in
    Result");
Col(customer, "Customer Unique ID", null, DataType.String, null);
Col(customer, "Customer Zip Prefix", null, DataType.String, null);
Col(customer, "Customer City", null, DataType.String, null);
Col(customer, "Customer State", null, DataType.String, null);

// --- Product -------------------------------------------------------------------
var product = Model.AddTable("Product", false);
product.Description = "One row per product, with the English category.";
product.AddMPartition("Product", @"let
    Products = " + Read("olist_products_dataset.csv") + @",
    Typed = Table.TransformColumnTypes(Products, {
        {""product_id"", type text}, {""product_category_name"", type text},
        {""product_name_lenght"", Int64.Type}, {""product_description_lenght"", Int64.Type}, {""product_photos_qty"", Int64.Type},
        {""product_weight_g"", Int64.Type}, {""product_length_cm"", Int64.Type}, {""product_height_cm"", Int64.Type}, {""product_width_cm"", Int64.Type}}, ""en-US""),
    TranslationFile = " + Read("product_category_name_translation.csv") + @",
    MissingTranslations = #table(type table [product_category_name = text, product_category_name_english = text], {
        {""pc_gamer"", ""pc_gamer""},
        {""portateis_cozinha_e_preparadores_de_alimentos"", ""portable_kitchen_and_food_preparers""}}),
    Translation = Table.Distinct(Table.Combine({TranslationFile, MissingTranslations}), {""product_category_name""}),
    Merged = Table.NestedJoin(Typed, {""product_category_name""}, Translation, {""product_category_name""}, ""Tr"", JoinKind.LeftOuter),
    Expanded = Table.ExpandTableColumn(Merged, ""Tr"", {""product_category_name_english""}),
    AddCategory = Table.AddColumn(Expanded, ""Category"", each
        if [product_category_name_english] = null or [product_category_name_english] = """" then ""Unknown""
        else Text.Proper(Text.Replace([product_category_name_english], ""_"", "" "")), type text),
    CategoryPt = Table.TransformColumns(AddCategory, {{""product_category_name"", each if _ = null or _ = """" then ""Unknown"" else _, type text}}),
    Renamed = Table.RenameColumns(CategoryPt, {
        {""product_id"", ""Product ID""}, {""product_category_name"", ""Category (Portuguese)""},
        {""product_name_lenght"", ""Product Name Length""}, {""product_description_lenght"", ""Product Description Length""},
        {""product_photos_qty"", ""Product Photos Qty""}, {""product_weight_g"", ""Product Weight (g)""},
        {""product_length_cm"", ""Product Length (cm)""}, {""product_height_cm"", ""Product Height (cm)""}, {""product_width_cm"", ""Product Width (cm)""}}),
    Result = Table.SelectColumns(Renamed, {
        ""Product ID"", ""Category"", ""Category (Portuguese)"",
        ""Product Name Length"", ""Product Description Length"", ""Product Photos Qty"",
        ""Product Weight (g)"", ""Product Length (cm)"", ""Product Height (cm)"", ""Product Width (cm)""})
in
    Result");
Col(product, "Product ID", null, DataType.String, null);
Col(product, "Category", null, DataType.String, null);
Col(product, "Category (Portuguese)", null, DataType.String, null);
Col(product, "Product Name Length", "Attributes", DataType.Int64, "0");
Col(product, "Product Description Length", "Attributes", DataType.Int64, "0");
Col(product, "Product Photos Qty", "Attributes", DataType.Int64, "0");
Col(product, "Product Weight (g)", "Attributes", DataType.Int64, "#,0");
Col(product, "Product Length (cm)", "Attributes", DataType.Int64, "0");
Col(product, "Product Height (cm)", "Attributes", DataType.Int64, "0");
Col(product, "Product Width (cm)", "Attributes", DataType.Int64, "0");

// --- Seller --------------------------------------------------------------------
var seller = Model.AddTable("Seller", false);
seller.Description = "One row per seller.";
seller.AddMPartition("Seller", @"let
    Sellers = " + Read("olist_sellers_dataset.csv") + @",
    Cleaned = Table.TransformColumns(Sellers, {
        {""seller_id"", each _, type text},
        {""seller_zip_code_prefix"", each Text.PadStart(_, 5, ""0""), type text},
        {""seller_city"", Text.Proper, type text},
        {""seller_state"", Text.Upper, type text}}),
    Result = Table.RenameColumns(Cleaned, {
        {""seller_id"", ""Seller ID""}, {""seller_zip_code_prefix"", ""Seller Zip Prefix""},
        {""seller_city"", ""Seller City""}, {""seller_state"", ""Seller State""}})
in
    Result");
Col(seller, "Seller ID", null, DataType.String, null);
Col(seller, "Seller Zip Prefix", null, DataType.String, null);
Col(seller, "Seller City", null, DataType.String, null);
Col(seller, "Seller State", null, DataType.String, null);

Output("Created DataPath + Orders (14 cols), Customer (4), Product (10), Seller (4).");
