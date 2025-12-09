# Fog Lamp Assembly Line Simulation

## Overview
This project simulates an automated assembly line for fog lamps. It uses a central SQL Server database to coordinate a Workstation Simulation (backend logic) and several WPF dashboards (frontend visualization).

## Setup Instructions

### 1. Database Setup
1. Open **SQL Server Management Studio (SSMS)**.
2. Open the file `DBqueries.sql`.
3. Execute the script to create the database, tables, and stored procedures.

### 2. Configure Connection Strings
Before running, you must point the applications to your specific SQL Server instance.

1. Open the solution in **Visual Studio**.
2. For **EVERY** project in the solution (WorkstationSim, RunnerDisplay, KanbanBoard, AndonDisplay, ConfigTool):
   * Open `App.config`.
   * Find the `<connectionStrings>` section.
   * Update `Server=YOUR_SERVER_NAME` to match your computer's SQL Server name.
   