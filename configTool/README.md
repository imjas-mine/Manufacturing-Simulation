# Configuration Tool 

A  Windows  application built with C# and WPF (.NET Framework) that allows users to view and edit configuration data stored in a SQL Server database.  
This tool is useful for administrators and developers who want to modify app configuration values without directly accessing the database.

---

# Features

- Fetches configuration data from a SQL Server database table  
- Allows inline editing directly in a DataGrid  
- Saves changes back to the database with a single click  

---

# Project Structure
configTool/
│
├── App.config # Contains database connection string
├── MainWindow.xaml # UI layout for the main window
├── MainWindow.xaml.cs # Backend logic (DB connection, loading, saving)
├── configTool.csproj # Project file
└── README.md # Project documentation

---

# Database Setup

Run these scripts:

CREATE DATABASE IF NOT EXISTS FogLampManufacturing;

CREATE TABLE Configuration (
    ConfigDescription NVARCHAR(50) PRIMARY KEY,
    Value NVARCHAR(50) NOT NULL
);

INSERT INTO Configuration (ConfigDescription, Value) VALUES
('TimeScale', '2'),
('Harness', '55'),
('Reflector', '35'),
('Housing', '24'),
('Lens', '40'),
('Bulb', '60'),
('Bezel', '75'),
('BinMin', '5'),
('RefreshSpan', '5'),
('AssemblyStations', '3'),
('OrderAmount', '1000');

---

# How to Run

- Open the project in Visual Studio

- In your App.config file, make sure to set your connection string correctly:

<connectionStrings>
  <add name="FogLampManufacturing"
       connectionString="Data Source=YOUR_SERVER_NAME;Initial Catalog=YOUR_DATABASE_NAME;Integrated Security=True;" 
       providerName="System.Data.SqlClient"/>
</connectionStrings>

- Make sure your SQL Server is running

- Press F5 (Run)

- The app will:
    Connect to the database
    Load all rows from the Configuration table
    Display them in the grid
    Let you edit and save changes