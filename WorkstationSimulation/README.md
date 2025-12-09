# Workstation Simulation
This project simulates a manufacturing workstation where orders flow through different stages of assembly. Each order has a specified quantity, and the workstation processes items one by one until the required quantity is completed. Once the assembly count reaches the order quantity, production for that order stops.

# Features

 - Order quantity control: The assembly stops automatically once the specified quantity is finished.
 - Step-by-step assembly: Each item moves through multiple workstation steps.
 - Status tracking: Displays the current count and which stage an item is in.
 - Prevents over-production: Ensures no extra items are manufactured beyond the order amount.


# Database Setup

Run the Milestone2.sql script provided with this project

# How to Run

- Open the project in Visual Studio

- In your App.config file, make sure to set your connection string correctly:

	<connectionStrings>
		<add name="FogLampDB"
			 connectionString="Server=<Your server name>;Database=FogLampManufacturing;Integrated Security=true;TrustServerCertificate=true;"
			 providerName="System.Data.SqlClient" />
	</connectionStrings>

- Make sure your SQL Server is running

- Press F5 (Run)

- The app will:
    Start the workstation simulator.
    It connects to the database using the connection string in App.config.
    The simulator reads the active order.
    It checks how many units need to be produced.
    Parts are consumed from the bin.
    If any part runs out, the simulator marks the bin as needing replenishment.
    If all parts are available, one assembly is completed.
    The assembly count in the database is updated.
    The simulator repeats  the steps until the order quantity is finished.
    When the order is complete, the workstation stops producing.
    