Add a `_Measures` table with display folders, descriptions and format strings:
Sales: Total Sales, Total Freight, Items Sold, Order Count, Average Order Value, Total Payments.
Delivery: Avg Delivery Days, On-Time Delivery %, Late Orders (use inactive Date relationships via USERELATIONSHIP where needed).
Customers: Unique Customers, Repeat Customer %.
Reviews: Avg Review Score, % 5-Star, % 1-Star.
Time Intelligence: Sales YTD, Sales PY, Sales YoY %, Sales 3M Moving Avg.
Run BPA afterward and fix issues. If model data permission allows, validate a few measures with DAX queries and tell me the numbers; otherwise tell me which to check in Desktop.
