// Group 4: relationships, hide keys, SummarizeBy = None, check names, descriptions.
// Relationships per docs/model-plan.md: all many-to-one, single direction.

Func<string, string, Column> C = (t, c) => Model.Tables[t].Columns[c];

// --- Relationships (from = many side, to = one side) ------------------------
Action<string, string, string, string, bool> Rel = (ft, fc, tt, tc, active) => {
    var r = Model.AddRelationship();
    r.FromColumn = C(ft, fc);
    r.ToColumn = C(tt, tc);
    r.FromCardinality = RelationshipEndCardinality.Many;
    r.ToCardinality = RelationshipEndCardinality.One;
    r.CrossFilteringBehavior = CrossFilteringBehavior.OneDirection;
    r.IsActive = active;
};
Rel("Order Items", "Order ID", "Orders", "Order ID", true);
Rel("Payments", "Order ID", "Orders", "Order ID", true);
Rel("Reviews", "Order ID", "Orders", "Order ID", true);
Rel("Order Items", "Product ID", "Product", "Product ID", true);
Rel("Order Items", "Seller ID", "Seller", "Seller ID", true);
Rel("Orders", "Customer Unique ID", "Customer", "Customer Unique ID", true);
Rel("Orders", "Purchase Date", "Date", "Date", true);
Rel("Orders", "Delivered Date", "Date", "Date", false);
Rel("Orders", "Estimated Delivery Date", "Date", "Date", false);

// --- Hide keys, foreign keys and sort keys -----------------------------------
var hide = new[] {
    "Orders|Order ID", "Orders|Customer Unique ID",
    "Orders|Purchase Date", "Orders|Delivered Date", "Orders|Estimated Delivery Date",
    "Customer|Customer Unique ID", "Product|Product ID", "Seller|Seller ID",
    "Order Items|Order ID", "Order Items|Order Item Number", "Order Items|Product ID", "Order Items|Seller ID",
    "Payments|Order ID", "Payments|Payment Sequential",
    "Reviews|Review ID", "Reviews|Order ID",
    "Date|Month Number", "Date|Year Month Number", "Date|Day of Week Number" };
foreach (var h in hide) { var p = h.Split('|'); C(p[0], p[1]).IsHidden = true; }

// --- SummarizeBy = None on every column (explicit measures only) -------------
int summarize = 0;
foreach (var col in Model.AllColumns)
    if (col.SummarizeBy != AggregateFunction.None) { col.SummarizeBy = AggregateFunction.None; summarize++; }

// --- Descriptions for visible columns that have none -------------------------
var desc = new Dictionary<string, string> {
    { "Customer|Customer City", "City of the customer's most recent order." },
    { "Customer|Customer State", "State (2-letter code) of the customer's most recent order." },
    { "Customer|Customer Zip Prefix", "First 5 digits of the zip code of the customer's most recent order." },
    { "Orders|Order Status", "Order status: Delivered, Shipped, Canceled, Unavailable, Invoiced, Processing, Created, Approved." },
    { "Orders|Purchase Timestamp", "When the order was placed." },
    { "Orders|Approved Timestamp", "When the payment was approved." },
    { "Orders|Carrier Handoff Timestamp", "When the order was handed to the logistics carrier." },
    { "Orders|Delivered Timestamp", "When the order was delivered to the customer." },
    { "Product|Category", "Product category in English (Unknown when missing)." },
    { "Product|Category (Portuguese)", "Original Portuguese category name (Unknown when missing)." },
    { "Product|Product Name Length", "Number of characters in the product name." },
    { "Product|Product Description Length", "Number of characters in the product description." },
    { "Product|Product Photos Qty", "Number of published product photos." },
    { "Product|Product Weight (g)", "Product weight in grams." },
    { "Product|Product Length (cm)", "Product length in centimetres." },
    { "Product|Product Height (cm)", "Product height in centimetres." },
    { "Product|Product Width (cm)", "Product width in centimetres." },
    { "Seller|Seller City", "Seller's city." },
    { "Seller|Seller State", "Seller's state (2-letter code)." },
    { "Seller|Seller Zip Prefix", "First 5 digits of the seller's zip code." },
    { "Payments|Payment Installments", "Number of installments chosen by the customer." },
    { "Reviews|Review Created Date", "Date the satisfaction survey was sent to the customer." },
    { "Reviews|Review Answer Timestamp", "When the customer answered the survey." },
    { "Date|Year", "Calendar year." },
    { "Date|Quarter", "Calendar quarter (Q1-Q4)." },
    { "Date|Month", "Month abbreviation (Jan-Dec), sorted by month number." },
    { "Date|Year Month", "Year and month (yyyy-mm)." },
    { "Date|Day of Week", "Weekday abbreviation (Mon-Sun), sorted Monday first." },
    { "Date|Is Weekend", "True for Saturday and Sunday." } };
foreach (var d in desc) { var p = d.Key.Split('|'); C(p[0], p[1]).Description = d.Value; }

// --- Name check: no source-style names left ----------------------------------
var bad = Model.AllColumns.Where(col => col.Name.Contains("_") || col.Name != col.Name.Trim()).Select(col => col.DaxObjectFullName).ToList();

Output("Relationships: " + Model.Relationships.Count + ", hidden: " + hide.Length +
       ", SummarizeBy changed: " + summarize + ", descriptions: " + desc.Count +
       ", bad names: " + (bad.Count == 0 ? "none" : string.Join(", ", bad)));

// --- Follow-up (run separately after BPA): IsAvailableInMdx = false on hidden keys ---
// var sortTargets = new HashSet<Column>(Model.AllColumns.Where(c => c.SortByColumn != null).Select(c => c.SortByColumn));
// foreach (var c in Model.AllColumns.Where(c => c.IsHidden && c.IsAvailableInMDX && !sortTargets.Contains(c)))
//     c.IsAvailableInMDX = false;
