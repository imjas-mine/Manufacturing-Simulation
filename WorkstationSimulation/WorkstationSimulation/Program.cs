/************************************************************
 * File Name: Program.cs
 * Author: Jasmine Kaur
 * Date: 2025-11-18
 * Description: This file contains the main workstation simulation logic for the Fog Lamp Manufacturing system. 
                The program connects to SQL Server, allows selecting a station, worker, and order, and
                simulates assembly processing using stored procedures,handling timing, defects, and order completion rules.
 ************************************************************/

using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace FogLampManufacturing.WorkstationSimulation
{
    class Program
    {
        private static string connectionString;
        private static int stationId;
        private static int workerId;
        private static int currentOrderId;
        private static CancellationTokenSource cts;
        private static bool isRunning = false;

        static async Task Main(string[] args)
        {
            Console.Title = "Fog Lamp Workstation Simulation";

            // Get connection string from app.config
            connectionString = ConfigurationManager.ConnectionStrings["FogLampDB"]?.ConnectionString;

            if (string.IsNullOrEmpty(connectionString))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: Connection string 'FogLampDB' not found in app.config");
                Console.WriteLine("Press any key to exit...");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=======================================================");
            Console.WriteLine("   FOG LAMP MANUFACTURING - WORKSTATION SIMULATION");
            Console.WriteLine("=======================================================");
            Console.ResetColor();
            Console.WriteLine();

            // Test connection
            if (!await TestConnection())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nFailed to connect to database. Press any key to exit.");
                Console.ResetColor();
                Console.ReadKey();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Connected to database successfully!");
            Console.ResetColor();

            // Select station
            await SelectStation();

            // Select worker
            await SelectWorker();

            // Select or create order (REQUIRED)
            await SelectOrder();

            cts = new CancellationTokenSource();

            // Start simulation
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Controls: [S] Start/Stop | [Q] Quit");
            Console.ResetColor();
            Console.WriteLine();

            Task simulationTask = null;

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true).Key;

                    if (key == ConsoleKey.Q)
                    {
                        if (isRunning)
                        {
                            cts.Cancel();
                            if (simulationTask != null) await simulationTask;
                        }
                        break;
                    }
                    else if (key == ConsoleKey.S)
                    {
                        if (!isRunning)
                        {
                            isRunning = true;
                            cts = new CancellationTokenSource();

                            simulationTask = RunSimulation(cts.Token);
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Simulation STARTED");
                            Console.ResetColor();
                        }
                        else
                        {
                            isRunning = false;
                            cts.Cancel();
                            if (simulationTask != null) await simulationTask;
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Simulation STOPPED");
                            Console.ResetColor();
                        }
                    }
                }

                await Task.Delay(100);
            }

            Console.WriteLine("\nExiting...");
        }

        /*-----------------------------------------------------------
        * Function Name: TestConnection
        * Description: Attempts to connect to the SQL Server database
        *              to verify that the connection string is valid.
        * Parameters: None
        * Returns: bool - True if connection succeeds, false otherwise.
        *-----------------------------------------------------------*/
        static async Task<bool> TestConnection()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    await conn.OpenAsync();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Connection error: {ex.Message}");
                Console.ResetColor();
                return false;
            }
        }

        /*-----------------------------------------------------------
         * Function Name: SelectStation
         * Description: Retrieves active stations and prompts the user
         *              to select one. Validates the station selection.
         * Parameters: None
         * Returns: Task
         *-----------------------------------------------------------*/
        static async Task SelectStation()
        {
            int activeStations = 3;
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand("SELECT Value FROM dbo.Configuration WHERE ConfigDescription = 'AssemblyStations'", conn))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && int.TryParse(result.ToString(), out int stations))
                    {
                        activeStations = stations;
                    }
                }
            }

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand("SELECT TOP (@ActiveStations) StationID, StationName FROM dbo.Station ORDER BY StationID", conn))
                {
                    cmd.Parameters.AddWithValue("@ActiveStations", activeStations);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        Console.WriteLine($"Available Stations (Active: {activeStations}):");
                        while (await reader.ReadAsync())
                        {
                            Console.WriteLine($"  [{reader["StationID"]}] {reader["StationName"]}");
                        }
                    }
                }
            }

            while (true)
            {
                Console.Write("\nSelect Station ID: ");
                if (int.TryParse(Console.ReadLine(), out stationId))
                {
                    // Verify station exists and is within active range
                    using (var conn = new SqlConnection(connectionString))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new SqlCommand("SELECT StationName FROM dbo.Station WHERE StationID = @StationID AND StationID <= @ActiveStations", conn))
                        {
                            cmd.Parameters.AddWithValue("@StationID", stationId);
                            cmd.Parameters.AddWithValue("@ActiveStations", activeStations);
                            var name = await cmd.ExecuteScalarAsync();
                            if (name != null)
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"Selected: {name}");
                                Console.ResetColor();
                                return;
                            }
                        }
                    }
                }
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Invalid station ID. Must be between 1 and {activeStations}.");
                Console.ResetColor();
            }
        }
        /*-----------------------------------------------------------
         * Function Name: SelectWorker
         * Description: Displays available workers, validates a worker
         *              selection, or creates a new worker when chosen.
         * Parameters: None
         * Returns: Task
         *-----------------------------------------------------------*/

        static async Task SelectWorker()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand("SELECT WorkerID, WorkerName, SkillLevel FROM dbo.Worker ORDER BY WorkerID", conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    Console.WriteLine("\nAvailable Workers:");
                    bool hasWorkers = false;
                    while (await reader.ReadAsync())
                    {
                        hasWorkers = true;
                        Console.WriteLine($"  [{reader["WorkerID"]}] {reader["WorkerName"]} ({reader["SkillLevel"]})");
                    }

                    if (!hasWorkers)
                    {
                        Console.WriteLine("  No workers found.");
                    }
                }
            }

            Console.WriteLine("  [N] Create New Worker");

            while (true)
            {
                Console.Write("\nSelect Worker ID (or N for new): ");
                string input = Console.ReadLine();

                if (input.ToUpper() == "N")
                {
                    await CreateNewWorker();
                    return;
                }

                if (int.TryParse(input, out workerId))
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new SqlCommand("SELECT WorkerName, SkillLevel FROM dbo.Worker WHERE WorkerID = @WorkerID", conn))
                        {
                            cmd.Parameters.AddWithValue("@WorkerID", workerId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"Selected: {reader["WorkerName"]} ({reader["SkillLevel"]})");
                                    Console.ResetColor();
                                    return;
                                }
                            }
                        }
                    }
                }
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid worker ID. Try again.");
                Console.ResetColor();
            }
        }

        /*-----------------------------------------------------------
         * Function Name: CreateNewWorker
         * Description: Prompts the user to enter a worker name and 
         *              skill level, then inserts the worker into the DB.
         * Parameters: None
         * Returns: Task
         *-----------------------------------------------------------*/
        static async Task CreateNewWorker()
        {
            Console.Write("Enter worker name: ");
            string name = Console.ReadLine();

            Console.WriteLine("\nSkill Levels:");
            Console.WriteLine("  [1] Rookie (90s assembly, 0.85% defect)");
            Console.WriteLine("  [2] Experienced (60s assembly, 0.50% defect)");
            Console.WriteLine("  [3] Super (51s assembly, 0.15% defect)");
            Console.Write("Select (1-3): ");

            string skillLevel = "Experienced";
            int? baseTime = null;
            double? defectRate = null;

            if (int.TryParse(Console.ReadLine(), out int skillChoice))
            {
                switch (skillChoice)
                {
                    case 1:
                        skillLevel = "Rookie";
                        baseTime = 90;
                        defectRate = 0.0085;
                        break;
                    case 3:
                        skillLevel = "Super";
                        baseTime = 51;
                        defectRate = 0.0015;
                        break;
                    default:
                        skillLevel = "Experienced";
                        baseTime = 60;
                        defectRate = 0.005;
                        break;
                }
            }

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(
                    "INSERT INTO dbo.Worker (WorkerName, SkillLevel, BaseAssemblyTimeSeconds, DefectRate) " +
                    "VALUES (@Name, @Skill, @BaseTime, @DefectRate); SELECT SCOPE_IDENTITY();", conn))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Skill", skillLevel);
                    cmd.Parameters.AddWithValue("@BaseTime", baseTime);
                    cmd.Parameters.AddWithValue("@DefectRate", defectRate);
                    workerId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Created worker: {name} ({skillLevel}) - ID: {workerId}");
            Console.ResetColor();
        }

        /*-----------------------------------------------------------
         * Function Name: SelectOrder
         * Description: Lists open orders and allows the user to either
         *              select an existing one or create a new order.
         * Parameters: None
         * Returns: Task
         *-----------------------------------------------------------*/
        static async Task SelectOrder()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(
                    "SELECT OrderID, OrderAmount, CompletedAmount, InProcessAmount, Status FROM dbo.[Order] WHERE Status = 'OPEN' ORDER BY OrderID", conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    Console.WriteLine("\nOpen Orders:");
                    bool hasOrders = false;
                    while (await reader.ReadAsync())
                    {
                        hasOrders = true;
                        int orderAmt = Convert.ToInt32(reader["OrderAmount"]);
                        int completed = Convert.ToInt32(reader["CompletedAmount"]);
                        int inProcess = Convert.ToInt32(reader["InProcessAmount"]);
                        int remaining = orderAmt - completed - inProcess;
                        Console.WriteLine($"  [{reader["OrderID"]}] Total: {orderAmt}, Completed: {completed}, In-Process: {inProcess}, Remaining: {remaining}");
                    }
                    if (!hasOrders)
                    {
                        Console.WriteLine("  No open orders.");
                    }
                }
            }

            while (true)
            {
                Console.Write("\nEnter Order ID (or Enter to create new): ");
                string orderInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(orderInput))
                {
                    int defaultOrderAmount = 100;
                    using (var conn = new SqlConnection(connectionString))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new SqlCommand(
                            "SELECT Value FROM dbo.Configuration WHERE ConfigDescription = 'OrderAmount'", conn))
                        {
                            var result = await cmd.ExecuteScalarAsync();
                            if (result != null && int.TryParse(result.ToString(), out int configAmount))
                            {
                                defaultOrderAmount = configAmount;
                            }
                        }
                    }

                    Console.Write($"Enter order amount (default {defaultOrderAmount}): ");
                    string amountInput = Console.ReadLine();
                    int amount = defaultOrderAmount;

                    if (!string.IsNullOrWhiteSpace(amountInput) && int.TryParse(amountInput, out int customAmount))
                    {
                        amount = customAmount;
                    }

                    using (var conn = new SqlConnection(connectionString))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new SqlCommand(
                            "INSERT INTO dbo.[Order] (OrderAmount, Status) VALUES (@Amount, 'OPEN'); SELECT SCOPE_IDENTITY();", conn))
                        {
                            cmd.Parameters.AddWithValue("@Amount", amount);
                            currentOrderId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        }
                    }
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Created Order #{currentOrderId} for {amount} units");
                    Console.ResetColor();
                    return;
                }
                else if (int.TryParse(orderInput, out int orderId))
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        await conn.OpenAsync();
                        using (var cmd = new SqlCommand(
                            "SELECT OrderAmount, CompletedAmount, InProcessAmount FROM dbo.[Order] WHERE OrderID = @OrderID AND Status = 'OPEN'", conn))
                        {
                            cmd.Parameters.AddWithValue("@OrderID", orderId);
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    currentOrderId = orderId;
                                    int orderAmt = Convert.ToInt32(reader["OrderAmount"]);
                                    int completed = Convert.ToInt32(reader["CompletedAmount"]);
                                    int inProcess = Convert.ToInt32(reader["InProcessAmount"]);
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"Working on Order #{currentOrderId} (Total: {orderAmt}, Completed: {completed}, In-Process: {inProcess})");
                                    Console.ResetColor();
                                    return;
                                }
                            }
                        }
                    }
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Order not found or not open. Try again.");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Invalid input. Try again.");
                    Console.ResetColor();
                }
            }
        }

        /*-----------------------------------------------------------
         * Function Name: RunSimulation
         * Description: Executes the continuous workstation simulation.
         *              Starts assemblies, waits for completion time,
         *              handles defects, and stops once order completes
         *              or user cancels.
         * Parameters:
         *      cancellationToken - Token to stop simulation gracefully.
         * Returns: Task
         *-----------------------------------------------------------*/
        static async Task RunSimulation(CancellationToken cancellationToken)
        {
            int assemblyCount = 0;
            int successCount = 0;
            int failureCount = 0;

            int baseAssemblyTime = 60;
            double timeScale = 1.0;

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();

                // Get worker's base assembly time
                using (var cmd = new SqlCommand(
                    "SELECT ISNULL(BaseAssemblyTimeSeconds, 60) FROM dbo.Worker WHERE WorkerID = @WorkerID", conn))
                {
                    cmd.Parameters.AddWithValue("@WorkerID", workerId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null) baseAssemblyTime = Convert.ToInt32(result);
                }

                // Get TimeScale from config
                using (var cmd = new SqlCommand(
                    "SELECT Value FROM dbo.Configuration WHERE ConfigDescription = 'TimeScale'", conn))
                {
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && double.TryParse(result.ToString(), out double scale))
                    {
                        timeScale = scale;
                    }
                }
            }

            Console.WriteLine($"Base assembly time: {baseAssemblyTime}s, Time scale: {timeScale}x");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Check if order is complete
                    bool orderComplete = await IsOrderComplete();
                    if (orderComplete)
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Order #{currentOrderId} is COMPLETE! Stopping simulation.");
                        Console.ResetColor();
                        isRunning = false;
                        break;
                    }

                    // Start assembly
                    int? assemblyId = await StartAssembly();

                    if (!assemblyId.HasValue)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Insufficient parts. Waiting...");
                        Console.ResetColor();
                        await Task.Delay(5000, cancellationToken);
                        continue;
                    }

                    assemblyCount++;

                    // Calculate assembly time with +/- 10% variance
                    Random rnd = new Random(Guid.NewGuid().GetHashCode());
                    double variance = 0.9 + (rnd.NextDouble() * 0.2); // 0.9 to 1.1
                    double actualSeconds = baseAssemblyTime * variance;
                    int delayMs = (int)((actualSeconds * 1000) / timeScale);

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Assembly #{assemblyCount} started (ID: {assemblyId}, {actualSeconds:F1}s @ {timeScale}x speed)");

                    // Simulate assembly time
                    await Task.Delay(delayMs, cancellationToken);

                    // Complete assembly
                    bool isDefective = await CompleteAssembly(assemblyId.Value);

                    if (isDefective)
                    {
                        failureCount++;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Assembly #{assemblyCount} DEFECTIVE | Success: {successCount} | Failed: {failureCount}");
                        Console.ResetColor();
                    }
                    else
                    {
                        successCount++;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Assembly #{assemblyCount} COMPLETED | Success: {successCount} | Failed: {failureCount}");
                        Console.ResetColor();
                    }
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
                    Console.ResetColor();
                    await Task.Delay(2000, cancellationToken);
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n======= Summary =========");
            Console.WriteLine($"Total: {assemblyCount}");
            Console.WriteLine($"Success: {successCount}");
            Console.WriteLine($"Defective: {failureCount}");
            if (assemblyCount > 0)
            {
                Console.WriteLine($"Yield: {(successCount * 100.0 / assemblyCount):F2}%");
            }
            Console.WriteLine($"===========================");
            Console.ResetColor();
        }

        /*-----------------------------------------------------------
         * Function Name: IsOrderComplete
         * Description: Checks the order's CompletedAmount and 
         *              InProcessAmount to determine if the order has 
         *              finished production.
         * Parameters: None
         * Returns: bool - True if order is complete, false otherwise.
         *-----------------------------------------------------------*/

        static async Task<bool> IsOrderComplete()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand(
                    "SELECT OrderAmount, CompletedAmount, InProcessAmount FROM dbo.[Order] WHERE OrderID = @OrderID", conn))
                {
                    cmd.Parameters.AddWithValue("@OrderID", currentOrderId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int orderAmount = Convert.ToInt32(reader["OrderAmount"]);
                            int completed = Convert.ToInt32(reader["CompletedAmount"]);
                            int inProcess = Convert.ToInt32(reader["InProcessAmount"]);

                            // Order is complete when completed + inProcess >= orderAmount
                            return (completed + inProcess) >= orderAmount;
                        }
                    }
                }
            }
            return false;
        }

        /*-----------------------------------------------------------
         * Function Name: StartAssembly
         * Description: Calls the stored procedure usp_StartAssembly to
         *              begin an assembly if parts are available.
         * Parameters: None
         * Returns: int? - The new AssemblyID, or null if unable to start.
         *-----------------------------------------------------------*/
        static async Task<int?> StartAssembly()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand("dbo.usp_StartAssembly", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@StationID", stationId);
                    cmd.Parameters.AddWithValue("@WorkerID", workerId);
                    cmd.Parameters.AddWithValue("@OrderID", currentOrderId);

                    var assemblyIdParam = new SqlParameter("@AssemblyID", SqlDbType.Int) { Direction = ParameterDirection.Output };
                    var successParam = new SqlParameter("@Success", SqlDbType.Bit) { Direction = ParameterDirection.Output };
                    var messageParam = new SqlParameter("@Message", SqlDbType.VarChar, 200) { Direction = ParameterDirection.Output };

                    cmd.Parameters.Add(assemblyIdParam);
                    cmd.Parameters.Add(successParam);
                    cmd.Parameters.Add(messageParam);
                    try
                    {

                        await cmd.ExecuteNonQueryAsync();

                        bool success = successParam.Value != DBNull.Value && Convert.ToBoolean(successParam.Value);
                        if (success && assemblyIdParam.Value != DBNull.Value)
                        {
                            return Convert.ToInt32(assemblyIdParam.Value);
                        }
                        else
                        {
                            return null;
                        }

                    }
                    catch(SqlException ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"SQL ERROR: {ex.Message}");
                        Console.ResetColor();
                        return null;
                    }
                }
            }
        }

        /*-----------------------------------------------------------
         * Function Name: CompleteAssembly
         * Description: Calls usp_CompleteAssembly to finalize an 
         *              assembly, then checks whether it was defective.
         * Parameters:
         *      assemblyId - ID of the assembly to complete.
         * Returns: bool - True if defective, false if successful.
         *-----------------------------------------------------------*/
        static async Task<bool> CompleteAssembly(int assemblyId)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand("dbo.usp_CompleteAssembly", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@AssemblyID", assemblyId);

                    var successParam = new SqlParameter("@Success", SqlDbType.Bit) { Direction = ParameterDirection.Output };
                    var messageParam = new SqlParameter("@Message", SqlDbType.VarChar, 200) { Direction = ParameterDirection.Output };

                    cmd.Parameters.Add(successParam);
                    cmd.Parameters.Add(messageParam);

                    await cmd.ExecuteNonQueryAsync();

                    // Check if defective
                    using (var checkCmd = new SqlCommand("SELECT IsDefective FROM dbo.AssemblyLog WHERE AssemblyID = @AssemblyID", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@AssemblyID", assemblyId);
                        var result = await checkCmd.ExecuteScalarAsync();
                        return result != null && Convert.ToBoolean(result);
                    }
                }
            }
        }
    }
}