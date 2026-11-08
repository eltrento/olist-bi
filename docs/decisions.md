# Business definitions and data decisions

Decided with the project owner after data exploration (prompt 01, 2026-11-07). Change only after asking.

## Data location
- CSVs live in `data/raw/archive/` (as extracted from the Kaggle zip). `DataPath` points there.

## Sales
- "Sales" = sum of item `price` (excl. freight), **excluding orders with status `canceled` or `unavailable`**. All other statuses count (delivered, shipped, invoiced, processing, created, approved).
- Freight is a separate measure; payments totals are reported separately.
- Profile figures: all items R$13,591,643.70; excluded R$97,242.96 on 467 orders; Sales R$13,494,400.74.

## Orders
- Order Count (and the denominator of Average Order Value) = orders that have items and are Sales orders (status not canceled/unavailable): 98,199.

## Date
- Keep the full range (purchases 2016-09-04 to 2018-10-17). Thin months at both ends are handled by slicing in the report, not by trimming data.
- Date table must cover every date column used in relationships (purchase, delivered, estimated delivery; estimates run to 2018-11-12).

## Customer
- Key = `customer_unique_id`. Location attributes (city, state, zip prefix) come from the customer's **most recent order** (39 customers span more than one state, 250 more than one zip).

## Reviews
- Grain of the Reviews fact: `review_id` + `order_id` (`review_id` alone is not unique).
- Order review score = average of that order's reviews (547 orders have 2-3 reviews).
- Overall average review score = average of the per-order averages (each order weighs equally). Profile: 4.0868 vs 4.0864 for a plain average of all reviews.
- Comment title/message text is not loaded; a `Has Comment` flag is kept.

## Product category
- Null category -> `Unknown` (610 products).
- Missing translations, added in the model:
  - `pc_gamer` -> `pc_gamer`
  - `portateis_cozinha_e_preparadores_de_alimentos` -> `portable_kitchen_and_food_preparers`

## Delivery
- An order counts as delivered when it has a delivered customer date (96,476), regardless of status (status and dates disagree on 14 orders).
- Late = delivered customer **date** > estimated delivery **date** (date-only comparison; the estimate is always midnight, so a timestamp comparison would mark same-day deliveries late).

## Measures (prompt 04, 2026-11-08)
- Total Freight and Total Payments use the same scope as Sales: canceled/unavailable orders excluded.
- Delivery measures (Avg Delivery Days, On-Time Delivery %, Late Orders) follow the **delivered date** on a time axis (`USERELATIONSHIP` on Orders[Delivered Date]).
- Unique Customers = customers with at least one counted order (Sales order with items) in the current filter.
- Repeat Customer % = share of those customers with 2+ counted orders **within the same filter** (period, state, ...), not lifetime.
- % 5-Star / % 1-Star = share of **reviews** (review rows), not of orders.
