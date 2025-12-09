# Fog Lamp Assembly Line Simulation

## 📖 Overview
This project is a full-stack simulation of an automated manufacturing floor for fog lamps. It demonstrates how software controls industrial workflows by integrating a backend simulation engine with real-time frontend dashboards.

The system uses a central SQL Server database to coordinate the assembly process across multiple workstations, tracking inventory levels in real-time and triggering automated replenishment (Runner) tasks when stock gets low.

## 🛠️ Tech Stack
* **Language:** C# (.NET Core / .NET Framework)
* **UI Framework:** WPF (Windows Presentation Foundation)
* **Database:** Microsoft SQL Server (Stored Procedures, Triggers, Views)
* **Architecture:** Database-Centric (Shared State via SQL)

## 🚀 Key Components
* **Workstation Simulator (Console):** The "brain" of the operation. It simulates the physical assembly process, consuming parts and producing finished lamps in real-time.
* **Kanban Board (WPF):** Visualizes inventory levels for every bin, helping track material flow.
* **Andon Display (WPF):** Monitors station health, alerting operators if a machine goes down or needs maintenance.
* **Auto-Runner (WPF):** A "smart" logistics system that automatically detects low inventory and refills bins without human intervention.
* **Configuration Tool:** Allows dynamic adjustment of simulation speed and failure rates.

---

## ⚙️ Setup Instructions

### 1. Database Setup
1. Open **SQL Server Management Studio (SSMS)**.
2. Open the file `DBqueries.sql` located in the root folder.
3. Execute the script to create the database, tables, and stored procedures.

### 2. Configure Connection Strings
Before running, you must point the applications to your specific SQL Server instance.

1. Open the solution in **Visual Studio**.
2. For **EVERY** project in the solution (`WorkstationSim`, `RunnerDisplay`, `KanbanBoard`, `AndonDisplay`, `ConfigTool`):
   * Open `App.config`.
   * Find the `<connectionStrings>` section.
   * Update `Server=YOUR_SERVER_NAME` to match your computer's SQL Server name.

