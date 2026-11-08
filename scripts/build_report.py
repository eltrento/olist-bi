"""Generate the PBIR pages for the Olist report (docs/report-plan.md) and validate them.

Usage:
    python scripts/build_report.py <out_dir>          # write pages/ into <out_dir> and validate
    python scripts/build_report.py --check <defn_dir> # validate an existing report definition folder

<out_dir> / <defn_dir> is a report `definition` folder (it gets pages/pages.json and
pages/<page>/page.json + visuals/<visual>/visual.json). Field references are checked
against the TMDL in olist-project.SemanticModel/definition/tables.
"""
import json
import re
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TMDL_TABLES = ROOT / "olist-project.SemanticModel" / "definition" / "tables"

SCHEMA = "https://developer.microsoft.com/json-schemas/fabric/item/report/definition"
PAGE_SCHEMA = f"{SCHEMA}/page/2.1.0/schema.json"
PAGES_SCHEMA = f"{SCHEMA}/pagesMetadata/1.1.0/schema.json"
VISUAL_SCHEMA = f"{SCHEMA}/visualContainer/2.4.0/schema.json"

W, H = 1280, 720
M = "_Measures"

# --- layout grid (see docs/report-plan.md) -------------------------------------
TITLE = (24, 12, 560, 44)
SLICER_1 = (836, 8, 200, 56)
SLICER_2 = (1056, 8, 200, 56)
ROW2_Y, ROW3_Y, ROW_H = 176, 448, 260
WIDE_L = (24, ROW2_Y, 732, ROW_H)
WIDE_R = (768, ROW2_Y, 488, ROW_H)
HALF_L = (24, ROW3_Y, 610, ROW_H)
HALF_R = (646, ROW3_Y, 610, ROW_H)
FULL_3 = (24, ROW3_Y, 1232, ROW_H)


def card_slots(n):
    gap = 12
    w = (W - 48 - gap * (n - 1)) // n
    return [(24 + i * (w + gap), 68, w, 96) for i in range(n)]


# --- query expression helpers ---------------------------------------------------
def col(table, name):
    return {"Column": {"Expression": {"SourceRef": {"Entity": table}}, "Property": name}}


def meas(name):
    return {"Measure": {"Expression": {"SourceRef": {"Entity": M}}, "Property": name}}


def ref(field):
    kind = "Column" if "Column" in field else "Measure"
    return field[kind]["Expression"]["SourceRef"]["Entity"], field[kind]["Property"]


def proj(field):
    table, name = ref(field)
    return {"field": field, "queryRef": f"{table}.{name}", "nativeQueryRef": name}


def lit(value):
    return {"expr": {"Literal": {"Value": value}}}


def text_lit(s):
    return lit("'" + s.replace("'", "''") + "'")


def title(text):
    return {"title": [{"properties": {"show": lit("true"), "text": text_lit(text)}}]}


# --- visual builders ---------------------------------------------------------------
def container(name, pos, visual, filters=None):
    x, y, w, h = pos
    v = {
        "$schema": VISUAL_SCHEMA,
        "name": name,
        "position": {"x": x, "y": y, "z": 0, "height": h, "width": w, "tabOrder": 0},
        "visual": visual,
    }
    if filters:
        v["filterConfig"] = {"filters": filters}
    return v


def chart(vtype, roles, caption=None, sort=None, objects=None):
    vis = {"visualType": vtype,
           "query": {"queryState": {r: {"projections": [proj(f) for f in fs]} for r, fs in roles.items()}},
           "drillFilterOtherVisuals": True}
    if sort:
        field, direction = sort
        vis["query"]["sortDefinition"] = {"sort": [{"field": field, "direction": direction}], "isDefaultSort": False}
    if objects:
        vis["objects"] = objects
    if caption:
        vis["visualContainerObjects"] = title(caption)
    return vis


def textbox(text):
    return {"visualType": "textbox",
            "objects": {"general": [{"properties": {"paragraphs": [
                {"textRuns": [{"value": text, "textStyle": {"fontWeight": "bold", "fontSize": "20pt"}}]}]}}]}}


def card(measure):
    return chart("card", {"Values": [meas(measure)]})


def slicer(field):
    return chart("slicer", {"Values": [field]},
                 objects={"data": [{"properties": {"mode": text_lit("Dropdown")}}]})


DATA_LABELS = {"labels": [{"properties": {"show": lit("true")}}]}


def top_n(name, table, column, measure, n):
    """Visual-level Top N filter on table[column] by measure."""
    return {
        "name": name,
        "field": col(table, column),
        "type": "TopN",
        "filter": {
            "Version": 2,
            "From": [
                {"Name": "subquery", "Type": 2, "Expression": {"Subquery": {"Query": {
                    "Version": 2,
                    "From": [{"Name": "t", "Entity": table, "Type": 0}, {"Name": "m", "Entity": M, "Type": 0}],
                    "Select": [{"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": column},
                                "Name": "field"}],
                    "OrderBy": [{"Direction": 2, "Expression": {"Measure": {
                        "Expression": {"SourceRef": {"Source": "m"}}, "Property": measure}}}],
                    "Top": n}}}},
                {"Name": "t", "Entity": table, "Type": 0},
            ],
            "Where": [{"Condition": {"In": {
                "Expressions": [{"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": column}}],
                "Table": {"SourceRef": {"Source": "subquery"}}}}}],
        },
    }


def equals_true(name, table, column):
    """Visual-level categorical filter table[column] = TRUE."""
    return {
        "name": name,
        "field": col(table, column),
        "type": "Categorical",
        "filter": {
            "Version": 2,
            "From": [{"Name": "t", "Entity": table, "Type": 0}],
            "Where": [{"Condition": {"In": {
                "Expressions": [{"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": column}}],
                "Values": [[{"Literal": {"Value": "true"}}]]}}}],
        },
    }


def gradient_background(measure):
    return {"values": [{
        "properties": {"backColor": {"solid": {"color": {"expr": {"FillRule": {
            "Input": meas(measure),
            "FillRule": {"linearGradient2": {
                "min": {"color": {"Literal": {"Value": "'#FFFFFF'"}}},
                "max": {"color": {"Literal": {"Value": "'#118DFF'"}}},
                "nullColoringStrategy": {"strategy": {"Literal": {"Value": "'asZero'"}}}}}}}}}}},
        "selector": {"data": [{"dataViewWildcard": {"matchingOption": 1}}], "metadata": f"{M}.{measure}"}}]}


# --- pages ---------------------------------------------------------------------------
YEAR = col("Date", "Year")
YEAR_MONTH = col("Date", "Year Month")
CATEGORY = col("Product", "Category")
CUST_STATE = col("Customer", "Customer State")
SELLER_STATE = col("Seller", "Seller State")


def page_exec():
    cards = ["Total Sales", "Order Count", "Average Order Value", "Unique Customers",
             "On-Time Delivery %", "Avg Review Score"]
    vs = [container("title", TITLE, textbox("Executive Summary")),
          container("slicerYear", SLICER_1, slicer(YEAR)),
          container("slicerState", SLICER_2, slicer(CUST_STATE))]
    vs += [container(f"card{i + 1}", pos, card(m)) for i, (m, pos) in enumerate(zip(cards, card_slots(6)))]
    vs += [
        container("salesTrend", WIDE_L, chart(
            "lineChart",
            {"Category": [YEAR_MONTH], "Y": [meas("Total Sales"), meas("Sales 3M Moving Avg")],
             "Tooltips": [meas("Sales PY"), meas("Sales YoY %")]},
            "Sales by purchase month", sort=(YEAR_MONTH, "Ascending"))),
        container("topCategories", WIDE_R, chart(
            "clusteredBarChart", {"Category": [CATEGORY], "Y": [meas("Total Sales")]},
            "Top 10 categories by sales", sort=(meas("Total Sales"), "Descending"), objects=DATA_LABELS),
            filters=[top_n("topCategory", "Product", "Category", "Total Sales", 10)]),
        container("salesByState", FULL_3, chart(
            "clusteredColumnChart", {"Category": [CUST_STATE], "Y": [meas("Total Sales")]},
            "Sales by customer state", sort=(meas("Total Sales"), "Descending"))),
    ]
    return "ExecutiveSummary", "Executive Summary", vs


def page_product():
    cards = ["Total Sales", "Items Sold", "Total Freight", "Average Order Value"]
    vs = [container("title", TITLE, textbox("Product & Category Performance")),
          container("slicerYear", SLICER_1, slicer(YEAR)),
          container("slicerCategory", SLICER_2, slicer(CATEGORY))]
    vs += [container(f"card{i + 1}", pos, card(m)) for i, (m, pos) in enumerate(zip(cards, card_slots(4)))]
    vs += [
        container("topCategories", WIDE_L, chart(
            "clusteredBarChart", {"Category": [CATEGORY], "Y": [meas("Total Sales")]},
            "Top 15 categories by sales", sort=(meas("Total Sales"), "Descending"), objects=DATA_LABELS),
            filters=[top_n("topCategory", "Product", "Category", "Total Sales", 15)]),
        container("categoryTrend", WIDE_R, chart(
            "lineChart", {"Category": [YEAR_MONTH], "Y": [meas("Total Sales")]},
            "Category sales trend (purchase month)", sort=(YEAR_MONTH, "Ascending"))),
        container("categoryTable", FULL_3, chart(
            "tableEx",
            {"Values": [CATEGORY, meas("Total Sales"), meas("Items Sold"), meas("Order Count"),
                        meas("Average Order Value"), meas("Total Freight")]},
            "Category detail", sort=(meas("Total Sales"), "Descending"))),
    ]
    return "ProductCategory", "Product & Category", vs


def page_delivery():
    cards = ["On-Time Delivery %", "Avg Delivery Days", "Late Orders", "Avg Review Score", "% 5-Star", "% 1-Star"]
    score = col("Reviews", "Review Score")
    vs = [container("title", TITLE, textbox("Delivery & Reviews")),
          container("slicerYear", SLICER_1, slicer(YEAR)),
          container("slicerState", SLICER_2, slicer(CUST_STATE))]
    vs += [container(f"card{i + 1}", pos, card(m)) for i, (m, pos) in enumerate(zip(cards, card_slots(6)))]
    vs += [
        container("deliveryTrend", WIDE_L, chart(
            "lineClusteredColumnComboChart",
            {"Category": [YEAR_MONTH], "Y": [meas("Avg Delivery Days")], "Y2": [meas("On-Time Delivery %")]},
            "Delivery by delivered month", sort=(YEAR_MONTH, "Ascending"))),
        container("daysByState", WIDE_R, chart(
            "clusteredBarChart",
            {"Category": [CUST_STATE], "Y": [meas("Avg Delivery Days")],
             "Tooltips": [meas("On-Time Delivery %"), meas("Late Orders")]},
            "Avg delivery days by customer state", sort=(meas("Avg Delivery Days"), "Descending"))),
        container("scoreDistribution", HALF_L, chart(
            "clusteredColumnChart", {"Category": [score], "Y": [meas("Review Count")]},
            "Review score distribution (purchase date)", sort=(score, "Ascending"), objects=DATA_LABELS)),
        container("scoreLateVsOnTime", HALF_R, chart(
            "clusteredColumnChart", {"Category": [col("Orders", "Is Late")], "Y": [meas("Avg Review Score")]},
            "Avg review score: late (True) vs on time (False)", objects=DATA_LABELS),
            filters=[equals_true("deliveredOnly", "Orders", "Is Delivered")]),
    ]
    return "DeliveryReviews", "Delivery & Reviews", vs


def page_customers():
    cards = ["Unique Customers", "Repeat Customer %", "Active Sellers", "Total Sales"]
    vs = [container("title", TITLE, textbox("Customers & Sellers")),
          container("slicerYear", SLICER_1, slicer(YEAR)),
          container("slicerSellerState", SLICER_2, slicer(SELLER_STATE))]
    vs += [container(f"card{i + 1}", pos, card(m)) for i, (m, pos) in enumerate(zip(cards, card_slots(4)))]
    vs += [
        container("customersByState", WIDE_L, chart(
            "clusteredColumnChart",
            {"Category": [CUST_STATE], "Y": [meas("Unique Customers")],
             "Tooltips": [meas("Repeat Customer %"), meas("Total Sales")]},
            "Customers by state", sort=(meas("Unique Customers"), "Descending"))),
        container("salesBySellerState", WIDE_R, chart(
            "clusteredBarChart", {"Category": [SELLER_STATE], "Y": [meas("Total Sales")]},
            "Sales by seller state", sort=(meas("Total Sales"), "Descending"))),
        container("topSellers", HALF_L, chart(
            "tableEx",
            {"Values": [col("Seller", "Seller Label"), col("Seller", "Seller City"), col("Seller", "Seller State"),
                        meas("Total Sales"), meas("Order Count"), meas("Items Sold")]},
            "Top 10 sellers", sort=(meas("Total Sales"), "Descending")),
            filters=[top_n("topSeller", "Seller", "Seller Label", "Total Sales", 10)]),
        container("stateFlows", HALF_R, chart(
            "pivotTable",
            {"Rows": [SELLER_STATE], "Columns": [CUST_STATE], "Values": [meas("Total Sales")]},
            "Sales: seller state (rows) to customer state (columns)",
            objects=gradient_background("Total Sales"))),
    ]
    return "CustomersSellers", "Customers & Sellers", vs


PAGES = [page_exec, page_product, page_delivery, page_customers]


# --- write -----------------------------------------------------------------------------
def write(defn: Path):
    pages_dir = defn / "pages"
    if pages_dir.exists():
        shutil.rmtree(pages_dir)
    pages_dir.mkdir(parents=True)
    order = []
    for build in PAGES:
        name, display, visuals = build()
        order.append(name)
        pdir = pages_dir / name
        (pdir / "visuals").mkdir(parents=True)
        dump(pdir / "page.json", {"$schema": PAGE_SCHEMA, "name": name, "displayName": display,
                                  "displayOption": "FitToPage", "height": H, "width": W})
        for z, v in enumerate(visuals):
            v["position"]["z"] = z * 1000
            v["position"]["tabOrder"] = z * 1000
            dump(pdir / "visuals" / v["name"] / "visual.json", v)
    dump(pages_dir / "pages.json", {"$schema": PAGES_SCHEMA, "pageOrder": order, "activePageName": order[0]})


def dump(path: Path, obj):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


# --- validate --------------------------------------------------------------------------
def model_fields():
    """{table: set(column and measure names)} from the TMDL files."""
    fields = {}
    name_re = r"(?:'((?:[^']|'')+)'|([^\s=']+))"
    for f in TMDL_TABLES.glob("*.tmdl"):
        text = f.read_text(encoding="utf-8")
        t = re.search(r"^table " + name_re, text, re.M)
        table = (t.group(1) or t.group(2)).replace("''", "'")
        names = set()
        for m in re.finditer(r"^\t(?:column|measure) " + name_re, text, re.M):
            names.add((m.group(1) or m.group(2)).replace("''", "'"))
        fields[table] = names
    return fields


def iter_field_refs(o):
    if isinstance(o, dict):
        for kind in ("Column", "Measure"):
            if kind in o and isinstance(o[kind], dict) and "Property" in o[kind]:
                src = o[kind]["Expression"]["SourceRef"]
                if "Entity" in src:
                    yield src["Entity"], o[kind]["Property"]
        for v in o.values():
            yield from iter_field_refs(v)
    elif isinstance(o, list):
        for v in o:
            yield from iter_field_refs(v)


def validate(defn: Path):
    errors = []
    fields = model_fields()
    pages_json = json.loads((defn / "pages" / "pages.json").read_text(encoding="utf-8"))
    for page in pages_json["pageOrder"]:
        pdir = defn / "pages" / page
        p = json.loads((pdir / "page.json").read_text(encoding="utf-8"))
        for k in ("$schema", "name", "displayName", "displayOption"):
            if k not in p:
                errors.append(f"{page}/page.json: missing {k}")
        rects = []
        for vf in sorted((pdir / "visuals").glob("*/visual.json")):
            v = json.loads(vf.read_text(encoding="utf-8"))
            where = f"{page}/{v.get('name')}"
            for k in ("$schema", "name", "position"):
                if k not in v:
                    errors.append(f"{where}: missing {k}")
            if v["name"] != vf.parent.name or len(v["name"]) > 50:
                errors.append(f"{where}: name must match folder and be <= 50 chars")
            pos = v["position"]
            if pos["x"] < 0 or pos["y"] < 0 or pos["x"] + pos["width"] > W or pos["y"] + pos["height"] > H:
                errors.append(f"{where}: outside the {W}x{H} canvas")
            rects.append((where, pos["x"], pos["y"], pos["x"] + pos["width"], pos["y"] + pos["height"]))
            for table, name in iter_field_refs(v):
                if table not in fields or name not in fields[table]:
                    errors.append(f"{where}: unknown field {table}[{name}]")
            for proj_state in v.get("visual", {}).get("query", {}).get("queryState", {}).values():
                refs = [pr["queryRef"] for pr in proj_state["projections"]]
                if len(refs) != len(set(refs)):
                    errors.append(f"{where}: duplicate queryRef")
        for i, a in enumerate(rects):
            for b in rects[i + 1:]:
                if a[1] < b[3] and b[1] < a[3] and a[2] < b[4] and b[2] < a[4]:
                    errors.append(f"{page}: {a[0]} overlaps {b[0]}")
        print(f"{page}: {len(rects)} visuals")
    return errors


if __name__ == "__main__":
    if len(sys.argv) == 3 and sys.argv[1] == "--check":
        target = Path(sys.argv[2])
    elif len(sys.argv) == 2:
        target = Path(sys.argv[1])
        write(target)
    else:
        sys.exit(__doc__)
    errs = validate(target)
    print("\n".join(errs) if errs else "Validation passed: JSON, required properties, field references, layout.")
    sys.exit(1 if errs else 0)
