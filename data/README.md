# Data: Olist Brazilian E-Commerce

Source: https://www.kaggle.com/datasets/olistbr/brazilian-ecommerce (Olist, ~100k orders, 2016-2018).
License on Kaggle is non-commercial (CC BY-NC-SA 4.0): fine for learning/portfolio; check before commercial use.

## Where things live
- `data/raw/archive/`: the 9 original CSVs, extracted here. **Never edited, never committed** (gitignored). Treat as read-only.
- `data/processed/`: optional cleaned/derived files (gitignored; rebuildable with scripts in `scripts/`).

## Get the data
1. Sign in to Kaggle, open the dataset page, click **Download** (a ~45 MB zip, ~120 MB unzipped).
2. Extract all CSVs into `data/raw/archive/` (the folder the Kaggle zip creates).
Alternative (optional): `pip install kaggle`, create an API token in Kaggle Settings, then
`kaggle datasets download -d olistbr/brazilian-ecommerce -p data/raw --unzip`.

## Expected files and grain (verify with `python scripts/profile_data.py`)
| File | Grain | Key columns |
|---|---|---|
| olist_orders_dataset.csv | one row per order | order_id, customer_id, order_status, purchase/approved/delivered/estimated timestamps |
| olist_order_items_dataset.csv | one row per item in an order | order_id + order_item_id, product_id, seller_id, price, freight_value |
| olist_order_payments_dataset.csv | one row per payment per order | order_id + payment_sequential, payment_type, payment_installments, payment_value |
| olist_order_reviews_dataset.csv | one row per review | review_id, order_id, review_score, comment fields |
| olist_customers_dataset.csv | one row per **customer_id** (per order!) | customer_id, customer_unique_id, zip prefix, city, state |
| olist_products_dataset.csv | one row per product | product_id, category (Portuguese), dimensions, weight |
| olist_sellers_dataset.csv | one row per seller | seller_id, zip prefix, city, state |
| olist_geolocation_dataset.csv | ~1M rows, many per zip prefix | zip prefix, lat, lng |
| product_category_name_translation.csv | category -> English | |

## Known gotchas (the interesting modeling parts)
- `customer_id` is unique per order; the real person is `customer_unique_id`. Customer dimension should key on `customer_unique_id`.
- Items, payments and reviews are all at different grains but all link to Orders by `order_id`. Orders is the hub dimension that filters all three fact tables.
- Order totals: sum of item price + freight does not always equal payments (vouchers, installments). Decide and document which "revenue" you report.
- Orders have 3+ date columns (purchase, delivered, estimated): the Date table is role-playing (one active relationship, others inactive with USERELATIONSHIP).
- Some orders have no items, no reviews, or several reviews; `review_id` is not perfectly unique.
- Geolocation has duplicates per zip prefix and is the biggest file: start without it, add later as a stretch goal.
- Some column names are misspelled in the source (e.g. `product_name_lenght`): rename in the model.
- Some categories are missing translations and some products have null categories.
