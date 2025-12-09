/*
File Name   : MainWindow.xaml.cs
Author      : Jasmine Kaur
Date        : 2025-12-04
Description : This file contains the logic for the Assembly Line Kanban Dashboard.
It retrieves high-level production metrics (Yield, Progress) and detailed status
for each workstation (Active Worker, Low Stock Alerts) from the SQL database. */
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Threading;

namespace KanbanDisplay
{
    public partial class MainWindow : Window
    {
        private string connectionString;
        private DispatcherTimer timer;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                connectionString = ConfigurationManager.ConnectionStrings["FogLampDB"]?.ConnectionString;

                // Setup Timer (Refresh every 2 seconds)
                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += Timer_Tick;
                timer.Start();

                RefreshData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup Error: " + ex.Message);
            }
        }

        /*-----------------------------------------------------------
     * Function Name: Timer_Tick
     * Description:   Event handler called by the timer every 2 seconds.
     * It triggers the data refresh function.
     * Parameters:    sender (object), e (EventArgs)
     * Returns:       None
     *-----------------------------------------------------------*/
        private void Timer_Tick(object sender, EventArgs e)
        {
            RefreshData();
        }

        /*-----------------------------------------------------------
             * Function Name: RefreshData
             * Description:   Connects to the SQL Database, runs queries to fetch
             * overall production metrics (via vw_KanbanOverview) and
             * station-specific status (Worker/Stock), and updates the UI.
             * Parameters:    None
             * Returns:       None
             *-----------------------------------------------------------*/
        private void RefreshData()
        {
            if (string.IsNullOrEmpty(connectionString)) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string sqlMain = @"
                        SELECT TOP 1 
                            OrderID, OrderAmount, CompletedAmount, InProcessAmount, 
                            ISNULL(YieldPercent, 100) as YieldPercent,
                            ISNULL(PercentComplete, 0) as PercentComplete
                        FROM dbo.vw_KanbanOverview
                        ORDER BY CASE WHEN CompletedAmount < OrderAmount THEN 0 ELSE 1 END, OrderID DESC";

                    var data = new KanbanData();

                    using (SqlCommand cmd = new SqlCommand(sqlMain, conn))
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            data.OrderId = Convert.ToInt32(rdr["OrderID"]);
                            data.OrderAmount = Convert.ToInt32(rdr["OrderAmount"]);
                            data.Completed = Convert.ToInt32(rdr["CompletedAmount"]);
                            data.InProcess = Convert.ToInt32(rdr["InProcessAmount"]);
                            data.YieldPercent = Convert.ToDecimal(rdr["YieldPercent"]);
                            data.ProgressPercent = Convert.ToDecimal(rdr["PercentComplete"]);
                        }
                    }


                    string sqlStations = @"
                        SELECT 
                            s.StationID,
                            s.StationName,
                            
                            -- Find the worker currently running a job at this station
                            (SELECT TOP 1 w.WorkerName 
                             FROM dbo.AssemblyLog al 
                             JOIN dbo.Worker w ON al.WorkerID = w.WorkerID 
                             WHERE al.StationID = s.StationID AND al.Status = 'IN_PROGRESS' 
                             ORDER BY al.StartTime DESC) as CurrentWorker,

                            -- Check if this station is working on the specific Active Order
                            (SELECT COUNT(*) 
                             FROM dbo.AssemblyLog al 
                             WHERE al.StationID = s.StationID 
                               AND al.Status = 'IN_PROGRESS' 
                               AND al.OrderID = @OrderID) as IsActiveOnOrder,

                            -- Check Stock
                            (SELECT MAX(CAST(IsLow AS INT)) FROM dbo.vw_BinStatus b WHERE b.StationID = s.StationID) as HasLowStock

                        FROM dbo.Station s";

                    using (SqlCommand cmd = new SqlCommand(sqlStations, conn))
                    {
                        cmd.Parameters.AddWithValue("@OrderID", data.OrderId);

                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                bool isLow = false;
                                if (rdr["HasLowStock"] != DBNull.Value)
                                    isLow = Convert.ToInt32(rdr["HasLowStock"]) == 1;

                                bool isActive = false;
                                if (rdr["IsActiveOnOrder"] != DBNull.Value)
                                    isActive = Convert.ToInt32(rdr["IsActiveOnOrder"]) > 0;

                                data.Stations.Add(new StationInfo
                                {
                                    StationName = rdr["StationName"].ToString(),
                                    WorkerName = rdr["CurrentWorker"] == DBNull.Value ? null : rdr["CurrentWorker"].ToString(),
                                    HasLowStock = isLow,
                                    IsActiveOnOrder = isActive
                                });
                            }
                        }
                    }

                    this.DataContext = data;
                }

                txtStatus.Text = "● SYSTEM ONLINE";
                txtStatus.Foreground = System.Windows.Media.Brushes.SpringGreen;
                txtLastUpdated.Text = $"Last Updated: {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "● DISCONNECTED";
                txtStatus.Foreground = System.Windows.Media.Brushes.Red;
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
    }
}