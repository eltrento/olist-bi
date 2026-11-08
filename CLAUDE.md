# Olist e-commerce BI project

Goal: build a star-schema Power BI semantic model from the Olist Brazilian E-Commerce dataset (9 CSVs in `data/raw/`, see `data/README.md`), then a Power BI report on top of it.

## Environment
- Windows. Tabular Editor 3 (3.27+) with its MCP server on http://127.0.0.1:42100/ (server name: `tabular-editor`).
- Power BI Desktop with Power BI Project (.pbip) save format enabled (TMDL + PBIR).
- Source of truth: the PBIP folders in `model/` (semantic model, TMDL) and `report/` (PBIR). These are text and tracked in git.
- Python available for profiling: `python scripts/profile_data.py`.

## Data rules
- `data/raw/` is READ-ONLY. Never edit, move, rename or delete files there. Never commit it (gitignored).
- Do not print large files into the conversation. Profile with scripts (pandas) and show summaries only.
- The model loads CSVs through Power Query with a text parameter `DataPath` (folder containing the CSVs, `data/raw/archive/`), so the path is changed in one place.
- Start WITHOUT the geolocation table (1M rows); add it later as an explicit step.

## Target model (approved 2026-11-07; full spec in `docs/model-plan.md`, business rules in `docs/decisions.md`)
- Fact tables: `Order Items` (grain: one item in an order), `Payments` (order payment), `Reviews` (order review).
- Dimensions: `Orders` (hub: status, delivery flags), `Customer` (key = customer_unique_id), `Product` (English category), `Seller`, `Date`.
- `Orders` filters all three fact tables (one-to-many, single direction). Product and Seller filter `Order Items`. Customer filters Orders via a customer unique key added to Orders (no direct Customer → Order Items: it would create an ambiguous path).
- `Date` is a marked date table (full calendar years) related to `Orders`: active on purchase date, inactive on delivered / estimated delivery date (use USERELATIONSHIP in measures).

## Model conventions
- Table and column names in Title Case with spaces, fix source typos (e.g. `product_name_lenght`).
- Measures live in a `_Measures` table, with display folders (Sales, Delivery, Customers, Reviews, Time Intelligence), format strings and descriptions.
- Hide key and foreign-key columns. Set `SummarizeBy = None` on numeric columns; use explicit measures only.
- Relationships: many-to-one, single direction; no bi-directional unless justified in writing.
- Revenue definition: default "Sales" = sum of item price (excl. freight), excluding canceled/unavailable orders. Freight is a separate measure. Payments totals are reported separately. Ask me before changing this.

## Working rules
- Read before changing: summarize the current model state first.
- Small steps. After each change tell me what changed, then run the Best Practice Analyzer.
- Show C# scripts before running if they create more than ~5 objects.
- Report work: PBIR edits only after validation; ask before big layout changes.
- Ask when a business definition is ambiguous instead of guessing.
- Commit to git after each completed step with a clear message.
