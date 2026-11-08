# Star schema plan (prompt 02)

Status: **approved 2026-11-07** (including the proposals for the former open questions, now in decisions.md). Business rules come from [decisions.md](decisions.md).
Current state: blank PBIP saved at the repo root (`olist-project.SemanticModel/`, `olist-project.Report/`); no tables built yet.

## Changes vs. the "Target model" in CLAUDE.md

1. **Drop the direct Customer → Order Items relationship.** With Customer → Orders and Orders → Order Items both active, a direct Customer → Order Items creates two filter paths (ambiguity), and Power BI would deactivate one. Customer filters every fact through Orders instead, which also lets Customer slice Payments and Reviews.
2. **Date relates to Orders** (the hub holds all three order dates), not to each fact. Filters flow Date → Orders → facts, and `USERELATIONSHIP` on the inactive Orders date columns works for every fact.
3. **Date range = full calendar years** from the first purchase year to the last year of any related date (2016-01-01 → 2018-12-31), not min/max purchase date. Estimated delivery dates run to 2018-11-12, past the last purchase (2018-10-17), so a purchase-only range would leave inactive-relationship rows without a date.

Everything else in the target model stands.

## Tables

`DataPath` = text parameter, e.g. `C:\Users\trent\Desktop\olist-bi\data\raw\archive\`. All CSVs read with `Csv.Document(File.Contents(DataPath & "<file>"), [Delimiter=",", Encoding=65001, QuoteStyle=QuoteStyle.Csv])`, promoted headers, explicit types (culture `en-US` for decimals/timestamps).
Key columns (`… ID`) are hidden; numeric columns get `SummarizeBy = None`.

### Orders (dimension, hub)
- Source: `olist_orders_dataset.csv`, merged with `olist_customers_dataset.csv` on `customer_id`.
- Grain: one order. Key: `Order ID` (99,441 rows).

| Source column | Model column | Type | Note |
|---|---|---|---|
| order_id | Order ID | text | key, hidden |
| customer_id | – | | dropped after the merge |
| (customers.customer_unique_id) | Customer Unique ID | text | FK → Customer, hidden |
| order_status | Order Status | text | Proper case: Delivered, Shipped, Canceled, … |
| order_purchase_timestamp | Purchase Timestamp | datetime | |
| (derived) | Purchase Date | date | FK → Date (active) |
| order_approved_at | Approved Timestamp | datetime | |
| order_delivered_carrier_date | Carrier Handoff Timestamp | datetime | |
| order_delivered_customer_date | Delivered Timestamp | datetime | |
| (derived) | Delivered Date | date | FK → Date (inactive) |
| order_estimated_delivery_date | Estimated Delivery Date | date | FK → Date (inactive) |
| (derived) | Is Sales Order | logical | status not Canceled/Unavailable |
| (derived) | Is Delivered | logical | Delivered Date not null |
| (derived) | Is Late | logical | Delivered Date > Estimated Delivery Date; null if not delivered |
| (derived) | Delivery Days | int | Delivered Date − Purchase Date; null if not delivered |

Power Query: read orders → type → read customers (only `customer_id`, `customer_unique_id`) → merge on customer_id → expand unique id → drop customer_id → add date columns (`DateTime.Date`) → add flags/days → rename → Proper-case status.

### Customer (dimension)
- Source: `olist_customers_dataset.csv` + `olist_orders_dataset.csv` (purchase timestamp, to pick the latest order).
- Grain: one person. Key: `Customer Unique ID` (96,096 rows).

| Source column | Model column | Note |
|---|---|---|
| customer_unique_id | Customer Unique ID | key, hidden |
| customer_zip_code_prefix | Customer Zip Prefix | text (keep leading zeros, 5 digits) |
| customer_city | Customer City | Proper case |
| customer_state | Customer State | 2-letter code |

Power Query: merge customers with orders on customer_id → sort by purchase timestamp desc → group by customer_unique_id keeping the first row (most recent order's location) → drop customer_id/timestamps → `Text.PadStart` zip to 5 → Proper-case city. 269 customers tie on latest timestamp, all with identical location, so the tie-break doesn't matter.

### Product (dimension)
- Source: `olist_products_dataset.csv` + `product_category_name_translation.csv` (not loaded).
- Grain: one product. Key: `Product ID` (32,951 rows).

| Source column | Model column | Note |
|---|---|---|
| product_id | Product ID | key, hidden |
| product_category_name | Category (Portuguese) | null → `Unknown` |
| (translation) | Category | English, `_` → space, Proper case (e.g. `Health Beauty`); null → `Unknown`; 2 missing translations added in M |
| product_name_lenght | Product Name Length | typo fixed |
| product_description_lenght | Product Description Length | typo fixed |
| product_photos_qty | Product Photos Qty | |
| product_weight_g | Product Weight (g) | |
| product_length_cm | Product Length (cm) | |
| product_height_cm | Product Height (cm) | |
| product_width_cm | Product Width (cm) | |

Power Query: read products → left-join translation on category → for the 2 untranslated codes use an inline `#table` mapping (`pc_gamer` → `pc_gamer`, `portateis_cozinha_e_preparadores_de_alimentos` → `portable_kitchen_and_food_preparers`) → null → Unknown → prettify → rename/type.

### Seller (dimension)
- Source: `olist_sellers_dataset.csv`. Grain: one seller. Key: `Seller ID` (3,095 rows).
- Columns: Seller ID (key, hidden), Seller Zip Prefix (text, 5 digits), Seller City (Proper case), Seller State.

### Date (dimension, marked date table)
- DAX calculated table: `CALENDAR(DATE(YEAR(MIN(Orders[Purchase Date])),1,1), DATE(YEAR(MAX(Orders[Estimated Delivery Date])),12,31))` → 2016-01-01 … 2018-12-31.
- Columns: Date (key, marked), Year, Quarter (`Q1`), Month Number, Month (`Jan`, sort by Month Number), Year Month (`2017-03`, sort by Year Month Number), Year Month Number, Day of Week, Day of Week Number, Is Weekend.

### Order Items (fact)
- Source: `olist_order_items_dataset.csv`. Grain: one item in an order. Key: `Order ID` + `Order Item Number` (112,650 rows).

| Source column | Model column | Note |
|---|---|---|
| order_id | Order ID | FK → Orders, hidden |
| order_item_id | Order Item Number | hidden |
| product_id | Product ID | FK → Product, hidden |
| seller_id | Seller ID | FK → Seller, hidden |
| shipping_limit_date | Shipping Limit Timestamp | not related to Date (4 rows in 2020) |
| price | Price | fixed decimal |
| freight_value | Freight Value | fixed decimal |

### Payments (fact)
- Source: `olist_order_payments_dataset.csv`. Grain: one payment of an order. Key: `Order ID` + `Payment Sequential` (103,886 rows).
- Columns: Order ID (FK, hidden), Payment Sequential (hidden), Payment Type (`credit_card` → `Credit Card`, `not_defined` → `Not Defined`, etc.), Payment Installments, Payment Value (fixed decimal).

### Reviews (fact)
- Source: `olist_order_reviews_dataset.csv`. Grain: one review of an order. Key: `Review ID` + `Order ID` (99,224 rows).
- Columns: Review ID (hidden), Order ID (FK, hidden), Review Score, Review Created Date, Review Answer Timestamp, Has Comment (logical: comment message not null). Comment title and message text are dropped.

## Relationships

All many-to-one, single direction (one side filters many side).

| From (many) | To (one) | Active | Purpose |
|---|---|---|---|
| Order Items[Order ID] | Orders[Order ID] | yes | |
| Payments[Order ID] | Orders[Order ID] | yes | |
| Reviews[Order ID] | Orders[Order ID] | yes | |
| Order Items[Product ID] | Product[Product ID] | yes | |
| Order Items[Seller ID] | Seller[Seller ID] | yes | |
| Orders[Customer Unique ID] | Customer[Customer Unique ID] | yes | Customer → Orders → all facts |
| Orders[Purchase Date] | Date[Date] | yes | default date |
| Orders[Delivered Date] | Date[Date] | **no** | `USERELATIONSHIP` in delivery measures |
| Orders[Estimated Delivery Date] | Date[Date] | **no** | `USERELATIONSHIP` in delivery measures |

## Ambiguity and behaviour risks

- **Product/Seller don't filter Payments or Reviews.** Payments and reviews are per order, not per item, so slicing by product or seller leaves payment and review measures unfiltered (they show the grand total). Visuals should not mix them; if needed later, a measure can push the filter through Order Items with `TREATAS`/`CROSSFILTER`, never a bi-directional relationship.
- **Snowflake through Orders.** Customer and Date reach facts via Orders (99k rows). Fine at this size; flattening keys into each fact is the alternative if performance ever needs it.
- **Orders without items (775), without reviews (768), without payment (1).** Order-level counts from Orders will exceed counts from Order Items; measure definitions must say which (open question 1).
- **Status vs. dates disagree on 14 orders** (8 "delivered" without a delivery date, 6 others with one). Delivery measures rely on Delivered Date, not status (open question 2).
- **Late by date vs. timestamp:** 6,535 late orders by date (the agreed rule) vs. 7,827 by timestamp.
- **Shipping Limit Timestamp** has 4 values in 2020, outside the Date table; it is not related to Date, so harmless.

## Resolved questions

1. **Order Count / AOV scope:** orders that have items and are Sales orders (98,199).
2. **Delivery measures base:** delivered = has a Delivered Date (96,476), regardless of status.
3. **Review comment text:** dropped; `Has Comment` flag kept.
