// Group 3: Date table (DAX calculated table), marked as date table.
// Range = full calendar years from the first purchase year to the last estimated-delivery year
// (docs/model-plan.md, change 3), so inactive relationships on delivered/estimated dates find a row.

var date = Model.AddCalculatedTable("Date", @"ADDCOLUMNS (
    CALENDAR (
        DATE ( YEAR ( MIN ( Orders[Purchase Date] ) ), 1, 1 ),
        DATE ( YEAR ( MAX ( Orders[Estimated Delivery Date] ) ), 12, 31 )
    ),
    ""Year"", YEAR ( [Date] ),
    ""Quarter"", ""Q"" & QUARTER ( [Date] ),
    ""Month Number"", MONTH ( [Date] ),
    ""Month"", FORMAT ( [Date], ""mmm"" ),
    ""Year Month"", FORMAT ( [Date], ""yyyy-mm"" ),
    ""Year Month Number"", YEAR ( [Date] ) * 100 + MONTH ( [Date] ),
    ""Day of Week"", FORMAT ( [Date], ""ddd"" ),
    ""Day of Week Number"", WEEKDAY ( [Date], 2 ),
    ""Is Weekend"", WEEKDAY ( [Date], 2 ) >= 6
)");
date.Description = "Calendar, full years covering purchase, delivered and estimated delivery dates. Marked as date table.";

// Calculated table columns are inferred from the DAX; they must exist before we can configure them.
if (date.Columns.Count == 0)
{
    Output("Date table created, but its columns are not inferred yet. Save (Ctrl+S) and run the configuration part again.");
    return;
}

var c = date.Columns;
c["Date"].FormatString = "yyyy-mm-dd";
c["Date"].IsKey = true;
c["Date"].Description = "One row per day. Key of the marked date table.";
c["Month"].SortByColumn = c["Month Number"];
c["Year Month"].SortByColumn = c["Year Month Number"];
c["Day of Week"].SortByColumn = c["Day of Week Number"];
c["Day of Week Number"].Description = "Monday = 1 ... Sunday = 7.";
foreach (var n in new[] { "Month Number", "Year Month Number", "Day of Week Number" }) c[n].DisplayFolder = "Sort Keys";
foreach (var n in new[] { "Year", "Month Number", "Year Month Number", "Day of Week Number" }) c[n].FormatString = "0";
date.DataCategory = "Time";   // mark as date table (Date column is the key)

Output("Date table created and marked: " + c.Count + " columns.");
