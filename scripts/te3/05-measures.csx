// Prompt 04: _Measures table with Sales, Delivery, Customers, Reviews and Time Intelligence measures.
// Definitions: docs/decisions.md. "Sales orders" = Orders[Is Sales Order] (not Canceled/Unavailable).

// --- Measure table: one hidden placeholder column, so only measures show -------
var mt = Model.AddCalculatedTable("_Measures", "{ BLANK () }");
mt.Description = "All explicit measures, grouped in display folders.";
if (mt.Columns.Count == 0) { Output("Placeholder column not inferred yet; save and re-run."); return; }
var ph = mt.Columns[0];
ph.IsHidden = true;
ph.SummarizeBy = AggregateFunction.None;
ph.IsAvailableInMDX = false;

const string Brl = "\\R$ #,0.00";
const string Int = "#,0";
const string Pct = "0.0%";

Action<string, string, string, string, string> M = (folder, name, dax, fmt, desc) => {
    var m = mt.AddMeasure(name, dax, folder);
    m.FormatString = fmt;
    m.Description = desc;
    m.FormatDax();
};

// --- Sales ------------------------------------------------------------------
M("Sales", "Total Sales",
  "CALCULATE ( SUM ( 'Order Items'[Price] ), KEEPFILTERS ( Orders[Is Sales Order] = TRUE () ) )", Brl,
  "Sum of item price (excl. freight) on Sales orders (status not Canceled/Unavailable).");
M("Sales", "Total Freight",
  "CALCULATE ( SUM ( 'Order Items'[Freight Value] ), KEEPFILTERS ( Orders[Is Sales Order] = TRUE () ) )", Brl,
  "Sum of item freight on Sales orders.");
M("Sales", "Items Sold",
  "CALCULATE ( COUNTROWS ( 'Order Items' ), KEEPFILTERS ( Orders[Is Sales Order] = TRUE () ) )", Int,
  "Number of order items on Sales orders.");
M("Sales", "Order Count",
  "CALCULATE ( DISTINCTCOUNT ( 'Order Items'[Order ID] ), KEEPFILTERS ( Orders[Is Sales Order] = TRUE () ) )", Int,
  "Number of Sales orders that have at least one item.");
M("Sales", "Average Order Value",
  "DIVIDE ( [Total Sales], [Order Count] )", Brl,
  "Total Sales / Order Count (excl. freight).");
M("Sales", "Total Payments",
  "CALCULATE ( SUM ( Payments[Payment Value] ), KEEPFILTERS ( Orders[Is Sales Order] = TRUE () ) )", Brl,
  "Sum of payment value on Sales orders. Includes freight; differs from Sales + Freight because of vouchers and installments.");

// --- Delivery (time axis = delivered date) -----------------------------------
const string Del = "USERELATIONSHIP ( Orders[Delivered Date], 'Date'[Date] )";
M("Delivery", "Avg Delivery Days",
  "CALCULATE ( AVERAGE ( Orders[Delivery Days] ), " + Del + " )", "0.0",
  "Average days from purchase to delivery, over delivered orders. Sliced by delivered date.");
M("Delivery", "Late Orders",
  "CALCULATE ( COUNTROWS ( Orders ), KEEPFILTERS ( Orders[Is Late] = TRUE () ), " + Del + " )", Int,
  "Delivered orders whose delivered date is after the estimated delivery date. Sliced by delivered date.");
M("Delivery", "On-Time Delivery %", @"
VAR Delivered = CALCULATE ( COUNTROWS ( Orders ), KEEPFILTERS ( Orders[Is Delivered] = TRUE () ), " + Del + @" )
RETURN DIVIDE ( Delivered - [Late Orders], Delivered )", Pct,
  "Share of delivered orders delivered on or before the estimated date. Sliced by delivered date.");

// --- Customers -----------------------------------------------------------------
const string CustTable = "CALCULATETABLE ( VALUES ( Orders[Customer Unique ID] ), 'Order Items', KEEPFILTERS ( Orders[Is Sales Order] = TRUE () ) )";
M("Customers", "Unique Customers",
  "COUNTROWS ( " + CustTable + " )", Int,
  "Distinct customers (customer_unique_id) with at least one counted order (Sales order with items).");
M("Customers", "Repeat Customer %", @"
VAR Customers = " + CustTable + @"
VAR Repeaters = COUNTROWS ( FILTER ( Customers, [Order Count] >= 2 ) )
RETURN DIVIDE ( Repeaters, COUNTROWS ( Customers ) )", Pct,
  "Share of Unique Customers with 2+ counted orders within the current filter.");

// --- Reviews -------------------------------------------------------------------
M("Reviews", "Avg Review Score",
  "AVERAGEX ( VALUES ( Reviews[Order ID] ), CALCULATE ( AVERAGE ( Reviews[Review Score] ) ) )", "0.00",
  "Average of per-order average review scores (each order weighs equally).");
M("Reviews", "% 5-Star",
  "DIVIDE ( CALCULATE ( COUNTROWS ( Reviews ), KEEPFILTERS ( Reviews[Review Score] = 5 ) ), COUNTROWS ( Reviews ) )", Pct,
  "Share of reviews with score 5.");
M("Reviews", "% 1-Star",
  "DIVIDE ( CALCULATE ( COUNTROWS ( Reviews ), KEEPFILTERS ( Reviews[Review Score] = 1 ) ), COUNTROWS ( Reviews ) )", Pct,
  "Share of reviews with score 1.");

// --- Time Intelligence (purchase date) -----------------------------------------
M("Time Intelligence", "Sales YTD",
  "CALCULATE ( [Total Sales], DATESYTD ( 'Date'[Date] ) )", Brl,
  "Total Sales year to date (by purchase date).");
M("Time Intelligence", "Sales PY",
  "CALCULATE ( [Total Sales], SAMEPERIODLASTYEAR ( 'Date'[Date] ) )", Brl,
  "Total Sales in the same period of the previous year.");
M("Time Intelligence", "Sales YoY %", @"
VAR PY = [Sales PY]
RETURN IF ( NOT ISBLANK ( PY ), DIVIDE ( [Total Sales] - PY, PY ) )", "+0.0%;-0.0%;0.0%",
  "Change of Total Sales vs. the same period last year. Blank when there are no prior-year sales.");
M("Time Intelligence", "Sales 3M Moving Avg", @"
CALCULATE (
    AVERAGEX ( VALUES ( 'Date'[Year Month Number] ), [Total Sales] ),
    DATESINPERIOD ( 'Date'[Date], MAX ( 'Date'[Date] ), -3, MONTH )
)", Brl,
  "Average monthly Total Sales over the 3 months ending with the last date in context (fewer months at the start of the data).");

Output("Created _Measures with " + mt.Measures.Count + " measures.");

// --- Follow-up (run separately after BPA): single-language model, ignore translation rules ---
// Model.SetAnnotation("BestPracticeAnalyzer_IgnoreRules",
//     "{\"RuleIDs\":[\"TE3_BUILT_IN_TRANSLATE_DISPLAY_FOLDERS\",\"TE3_BUILT_IN_TRANSLATE_DESCRIPTIONS\",\"TE3_BUILT_IN_TRANSLATE_PERSPECTIVES\",\"TE3_BUILT_IN_TRANSLATE_VISIBLE_NAMES\"]}");
