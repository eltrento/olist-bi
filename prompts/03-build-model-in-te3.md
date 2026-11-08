Use the tabular-editor MCP server. Tell me which model is open and what it contains.
Then, in this order, with a pause for my OK after each numbered group:
1. Create the `DataPath` parameter and the dimension tables (Orders, Customer, Product, Seller) with Power Query (M) partitions reading the CSVs from DataPath.
2. Create the fact tables (Order Items, Payments, Reviews).
3. Create the Date table (DAX CALENDAR from min/max purchase date), mark as date table.
4. Create relationships, hide keys, set SummarizeBy = None, rename columns.
Show me the C# script before running anything that creates more than ~5 objects. Afterward run BPA and report issues. Do not process (refresh) data unless I ask; tell me how I can.
