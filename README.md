# Olist e-commerce: dataset → semantic model → Power BI report, with Claude Code

Workflow: **Kaggle CSVs → (Claude Code) profile & plan → (Claude Code + Tabular Editor 3 MCP) semantic model → (Claude Code + PBIP/PBIR) Power BI report**

## Part 0: Setup (once)

1. **Claude Code.** Either use the **Code tab in the Claude desktop app** (pick this project folder), or install the CLI in PowerShell: `irm https://claude.ai/install.ps1 | iex`, then `claude --version`.
   You still need the `claude` command in a terminal for one step later (registering Tabular Editor's MCP server). If `claude --version` fails, run the installer above.
2. **Tabular Editor 3**, version 3.27.0 or later (MCP server is in every edition).
3. **Power BI Desktop**: File > Options > Preview features > enable *Power BI Project (.pbip)* save format (and PBIR if offered).
4. **Python** with pandas (`pip install pandas`), used only to profile the data.
5. Unzip this project somewhere like `C:\Projects\olist-bi`. Install Git for Windows if needed (`git --version`).
6. Open Claude Code in this folder and say: *"Initialize a git repo here, check .gitignore is in place, and make an initial commit. From now on, commit after each completed step with a clear message."*

## Part 1: Get and store the data

How the data is handled (decisions already made for you):
- **Download once** from Kaggle (~45 MB zip, ~120 MB unzipped, ~100k orders). Not truly huge, so Power BI can import it directly.
- **Store** the 9 CSVs in `data/raw/`. That folder is gitignored (git is for code and model definitions, not data) and treated as read-only by `CLAUDE.md`.
- **Load** via Power Query in the model from one `DataPath` parameter, so moving the folder means changing one value.
- **Profile** with `scripts/profile_data.py`, so Claude reads summaries instead of 1M-row files.
- The 1M-row **geolocation** file is skipped at first; add it as a stretch goal.

Steps:
1. Sign in to Kaggle, open https://www.kaggle.com/datasets/olistbr/brazilian-ecommerce and click **Download**.
2. Extract all CSVs into `data/raw/archive/` (the folder the Kaggle zip creates).
3. Read `data/README.md` for table grains and known gotchas.

## Part 2: Claude Code basics (10 minutes)

| Thing | What it does |
|---|---|
| `CLAUDE.md` | Project memory read at the start of every session. Already written for this dataset: target model, naming rules, "ask me when ambiguous." Edit it whenever Claude gets something wrong. |
| Plan mode (Shift+Tab to cycle modes in the terminal) | Claude proposes before editing. Use for anything big. |
| Permission prompts | Claude asks before edits and commands. Say "allow once" while learning. |
| `.claude/settings.json` | Pre-approves safe actions, blocks dangerous ones. |
| `/clear`, `/compact` | Reset or shrink the conversation when it gets long. |
| `/mcp` | Shows connected MCP servers (like Tabular Editor). |

Habits: be specific about the goal; plan first on big tasks; commit before and after big steps; review diffs; keep prompts in `prompts/` and refine them like code.

## Part 3: Explore and plan

Run these in order (type `Read prompts/01-explore-data.md and do it`):
1. `prompts/01-explore-data.md`: profile the CSVs, surface surprises.
2. `prompts/02-plan-star-schema.md`: confirm the model design; you approve before anything is built.

## Part 4: Build the semantic model in Tabular Editor 3

1. In TE3, create a new empty model (or open a model in a PBIP's `*.SemanticModel` folder). Save into `model/` as TMDL so it's git-friendly.
2. **Tools > MCP Server...** Set *Model metadata* to **Read/Write**. Leave *Model data* on Deny unless you want Claude to run DAX checks (data values then go to the model). Click **Start server**. Default address: `http://127.0.0.1:42100/`.
3. In a terminal: `claude mcp add --transport http tabular-editor http://127.0.0.1:42100/` (or copy it from the dialog's *Export configuration*, which is required if you enable an access token).
4. Restart your Claude Code session, run `/mcp`, and ask: "Which model is open in Tabular Editor?"
5. Run `prompts/03-build-model-in-te3.md`, then `prompts/04-measures.md`.
6. Permissions are read when the server starts: after changing them, stop/start the server and reconnect.
7. Scripts that reach outside the model (e.g. write files) are not run; TE3 opens them in an "Agent script (review)" tab for you to inspect. Each agent script is a single undo step (Ctrl+Z).
8. Data refresh: building the model doesn't load data. Process/refresh the tables in TE3 or Power BI Desktop. Update the `DataPath` parameter first to your `data\raw\archive` folder.

TE3's built-in AI Assistant pane (bring your own API key) is useful for quick in-UI questions; use Claude Code via MCP for multi-step builds.

## Part 5: Build the report

1. Save the model as a PBIP so you have `*.SemanticModel` and `*.Report` (PBIR) folders in the project. Easiest: create a blank report in Power BI Desktop connected to the model and save as PBIP here.
2. Run `prompts/05-build-report.md`. Claude plans, you approve, it writes the PBIR files, then you reload in Desktop and give feedback ("cards are cramped, move the slicer left").
3. Optional tooling (check current install steps and review what a plugin does before installing): Microsoft's Skills for Fabric Power BI report authoring skills (includes a PBIR validator and a Desktop bridge CLI), and the community marketplace `claude plugin marketplace add data-goblin/power-bi-agentic-development`.
4. Don't edit files that Desktop has open and then save from Desktop: reload first, or you'll overwrite Claude's changes.

## Part 6: Level up
- Add the geolocation table and a map page.
- Skills (`.claude/skills/`) for your DAX/naming standards; a reviewer subagent to audit against BPA; hooks to validate after each edit.

## Safety notes
- Work on this model locally; keep Model data permission on Deny unless needed.
- Olist data license is non-commercial (CC BY-NC-SA 4.0): fine for learning or a portfolio.
- Commit often; git is your undo.
