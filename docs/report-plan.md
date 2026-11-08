# Report plan (prompt 05)

Status: **approved 2026-11-08 with the defaults**: Seller Label column (`Seller 3442f8 (Campinas, SP)`), Review Count + Active Sellers measures, Sales YoY % tooltip-only. Slicers are dropdowns.
Current state: `olist-project.Report/` is the blank PBIR Desktop created: one empty page ("Page 1", 1280×720, FitToPage), base theme CY26SU05.

## Model facts that shape the report
- **Product and Seller filter only Order Items.** Sales measures react to them; Payments, Reviews and Delivery measures do not (they show the overall value). So no review or delivery visuals on the product/seller visuals, and no Category slicer on page 3.
- **Two date axes.** Sales, Customers, Reviews and Time Intelligence follow purchase date; Delivery measures follow delivered date (`USERELATIONSHIP`). A Year slicer on page 3 therefore means "delivered in" for delivery visuals and "purchased in" for review visuals. Visual titles say which.
- **Sales YoY % is distorted at year level** (2016 is Sep–Dec only, 2018 ends in Oct). The plan uses no year-level YoY; it appears only as a monthly tooltip.
- **No map.** 2-letter state codes geocode poorly and geolocation isn't loaded yet; states are shown as sorted bar charts. A map can come with the geolocation step.
- **Sellers have no name**, only a hex ID, city and state (see open question 1).

## Common to all pages
- Canvas 1280×720, FitToPage, base theme. Title text box top-left, slicers in a band on the top right, KPI cards in a row under the title, two rows of charts below.
- Slicers are page-level (not synced) unless noted. Default cross-filtering between visuals.
- All values are explicit measures from `_Measures`; no implicit aggregation.

```
+------------------------------------------------------------+
| Title                                  [Slicer] [Slicer]   |
| [KPI] [KPI] [KPI] [KPI] [KPI] [KPI]                        |
| [ wide chart (60%)            ] [ chart (40%)           ]  |
| [ chart (50%)              ] [ chart (50%)              ]  |
+------------------------------------------------------------+
```

## 1. Executive Summary
| Visual | Fields |
|---|---|
| 6 cards | Total Sales, Order Count, Average Order Value, Unique Customers, On-Time Delivery %, Avg Review Score |
| Line chart "Sales by purchase month" | Axis Date[Year Month]; values Total Sales, Sales 3M Moving Avg; tooltip Sales PY, Sales YoY % |
| Bar chart "Top 10 categories" | Axis Product[Category]; value Total Sales; Top N 10 by Total Sales |
| Bar chart "Sales by customer state" | Axis Customer[Customer State]; value Total Sales; sorted descending |
| Slicers | Date[Year] (tile), Customer[Customer State] (dropdown) |

## 2. Product & Category Performance
| Visual | Fields |
|---|---|
| 4 cards | Total Sales, Items Sold, Total Freight, Average Order Value |
| Bar chart "Top 15 categories by sales" | Axis Product[Category]; value Total Sales; Top N 15 |
| Line chart "Category sales trend" | Axis Date[Year Month]; value Total Sales (reacts to the Category slicer / bar selection) |
| Table "Category detail" | Product[Category], Total Sales, Items Sold, Order Count, Average Order Value, Total Freight; sorted by Total Sales |
| Slicers | Date[Year], Product[Category] (dropdown with search) |

No review or delivery measures on this page (Product doesn't filter them).

## 3. Delivery & Reviews
| Visual | Fields |
|---|---|
| 6 cards | On-Time Delivery %, Avg Delivery Days, Late Orders, Avg Review Score, % 5-Star, % 1-Star |
| Line chart "Delivery by delivered month" | Axis Date[Year Month]; values On-Time Delivery % (Y1), Avg Delivery Days (Y2) |
| Bar chart "Avg delivery days by customer state" | Axis Customer[Customer State]; value Avg Delivery Days; tooltip On-Time Delivery %, Late Orders; sorted descending |
| Column chart "Review score distribution" | Axis Reviews[Review Score] (1-5); value **Review Count** (new measure, question 2) |
| Column chart "Review score: late vs on time" | Axis Orders[Is Late]; value Avg Review Score |
| Slicers | Date[Year], Customer[Customer State] |

## 4. Customers & Sellers
| Visual | Fields |
|---|---|
| 4 cards | Unique Customers, Repeat Customer %, **Active Sellers** (new measure, question 2), Total Sales |
| Bar chart "Customers by state" | Axis Customer[Customer State]; values Unique Customers; tooltip Repeat Customer %, Total Sales |
| Table "Top 10 sellers" | seller identifier (question 1), Seller City, Seller State, Total Sales, Order Count, Items Sold; Top N 10 by Total Sales |
| Bar chart "Sales by seller state" | Axis Seller[Seller State]; value Total Sales |
| Matrix "Seller state → customer state" | Rows Seller[Seller State]; columns Customer[Customer State]; value Total Sales; conditional-format background |
| Slicers | Date[Year], Seller[Seller State] |

## Open questions
1. **Seller identifier for "Top 10 sellers".** Options: (a) unhide `Seller[Seller ID]` (32-char hex); (b) add a short `Seller Label` column in Power Query, e.g. `Seller 3442f8 (Campinas, SP)`; (c) show only city/state (ambiguous: many sellers per city).
2. **Two new measures** in `_Measures`: `Review Count` = COUNTROWS(Reviews) (Reviews folder) and `Active Sellers` = distinct sellers on Sales orders (Customers folder, or a new Sellers folder).
3. **Sales YoY %:** keep it tooltip-only as above, or fix the measure first (blank for incomplete years / after the last sales date).

## Build and validation (after approval)
- Create 4 page folders under `olist-project.Report/definition/pages/` (replacing the empty Page 1) with `page.json` and one `visual.json` per visual, update `pages.json`.
- Validate: every JSON file parses and declares its `$schema`; every field reference exists in the model (checked against the TMDL); visual positions stay inside 1280×720 without overlaps.
- You close the project in Desktop before I write the files, then reopen `olist-project.pbip` and check each page; I commit after you confirm.
