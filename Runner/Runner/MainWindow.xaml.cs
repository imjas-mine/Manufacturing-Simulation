/*
 * File Name: MainWindow.xaml.cs
 * Author: Jasmine Kaur
 * Date: 2025-12-05
 * Description: This file contains the logic for the Runner Replenishment Dashboard.
 * It monitors the database for low-stock bins and automatically replenishes
 * them every 5 minutes if tasks exist.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Linq;
using Microsoft.Data.SqlClient;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;

namespace RunnerDisplay
{
    
    public partial class MainWindow : Window
    {
        private string connectionString;
        private DispatcherTimer timer;

        private int secondsUntilNextRun = 300; 
        private double timeScale = 1.0;

        public ObservableCollection<ReplenishItem> taskList { get; set; } = new ObservableCollection<ReplenishItem>();

        /*-----------------------------------------------------------
         * Function Name: MainWindow (Constructor)
         * Description:   Initializes the window, loads configuration,
         * sets up the timer, and loads initial data.
         * Parameters:    None
         * Returns:       None
         *-----------------------------------------------------------*/
        public MainWindow()
        {
            try
            {
                InitializeComponent();

                TaskList.ItemsSource = taskList;

                connectionString = ConfigurationManager.ConnectionStrings["FogLampDB"]?.ConnectionString;

                LoadTimeScale();

                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(1000 / timeScale);
                timer.Tick += Timer_Tick;
                timer.Start();

                LoadTasks();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup Error: " + ex.Message);
            }
        }

        /*-----------------------------------------------------------
         * Function Name: LoadTimeScale
         * Description:   Reads the 'TimeScale' value from the Configuration table.
         * Parameters:    None
         * Returns:       None
         *-----------------------------------------------------------*/
        private void LoadTimeScale()
        {
            if (string.IsNullOrEmpty(connectionString)) return;
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string sql = "SELECT Value FROM dbo.Configuration WHERE ConfigDescription = 'TimeScale'";
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        object result = cmd.ExecuteScalar();
                        if (result != null && double.TryParse(result.ToString(), out double scale))
                        {
                            timeScale = scale <= 0 ? 1.0 : scale;
                        }
                    }
                }
            }
            catch
            {
                timeScale = 1.0;
            }
        }

        /*-----------------------------------------------------------
         * Function Name: Timer_Tick
         * Description:   Called every "simulated second". Updates the countdown.
         * When it hits 0, it automatically replenishes all pending tasks
         * and resets the timer.
         * Parameters:    sender (object), e (EventArgs)
         * Returns:       None
         *-----------------------------------------------------------*/
        private void Timer_Tick(object sender, EventArgs e)
        {
            LoadTasks();

            secondsUntilNextRun--;

            if (secondsUntilNextRun <= 0)
            {
                if (taskList.Count > 0)
                {
                    PerformAutoReplenishment();

                    LoadTasks();
                }

                secondsUntilNextRun = 300;
            }

            TimeSpan time = TimeSpan.FromSeconds(secondsUntilNextRun);
            txtTimer.Text = time.ToString(@"mm\:ss");

            if (secondsUntilNextRun <= 10)
                txtTimer.Foreground = Brushes.OrangeRed;
            else
                txtTimer.Foreground = Brushes.White;
        }

        /*-----------------------------------------------------------
         * Function Name: PerformAutoReplenishment
         * Description:   Loops through all items in the taskList and 
         * executes the replenishment stored procedure for each.
         * Parameters:    None
         * Returns:       None
         *-----------------------------------------------------------*/
        private void PerformAutoReplenishment()
        {
            if (string.IsNullOrEmpty(connectionString)) return;

            var itemsToReplenish = taskList.ToList();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                try
                {
                    conn.Open();

                    foreach (var item in itemsToReplenish)
                    {
                        using (SqlCommand cmd = new SqlCommand("dbo.usp_ReplenishBin", conn))
                        {
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@BinID", item.BinID);
                            cmd.Parameters.AddWithValue("@RunnerName", "AutoRunner"); 

                            SqlParameter pNewQty = new SqlParameter("@NewQuantity", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output };
                            SqlParameter pSuccess = new SqlParameter("@Success", System.Data.SqlDbType.Bit) { Direction = System.Data.ParameterDirection.Output };

                            cmd.Parameters.Add(pNewQty);
                            cmd.Parameters.Add(pSuccess);

                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Auto-Replenish Error: " + ex.Message);
                }
            }
        }

        /*-----------------------------------------------------------
         * Function Name: LoadTasks
         * Description:   Queries the database for bins where IsLow = 1.
         * Updates the list using Smart Update logic.
         * Parameters:    None
         * Returns:       None
         *-----------------------------------------------------------*/
        private void LoadTasks()
        {
            if (string.IsNullOrEmpty(connectionString)) return;

            var newTasks = new List<ReplenishItem>();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string sql = @"
                        SELECT b.BinID, b.StationID, b.PartName, b.CurrentQuantity, b.DefaultCapacity, s.StationName
                        FROM dbo.vw_BinStatus b
                        JOIN dbo.Station s ON b.StationID = s.StationID
                        WHERE b.IsLow = 1
                        ORDER BY b.StationID, b.PartName";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            newTasks.Add(new ReplenishItem
                            {
                                BinID = Convert.ToInt32(rdr["BinID"]),
                                StationID = Convert.ToInt32(rdr["StationID"]),
                                StationName = rdr["StationName"].ToString(),
                                PartName = rdr["PartName"].ToString(),
                                CurrentQuantity = Convert.ToInt32(rdr["CurrentQuantity"]),
                                MaxCapacity = Convert.ToInt32(rdr["DefaultCapacity"])
                            });
                        }
                    }
                }

                for (int i = taskList.Count - 1; i >= 0; i--)
                {
                    var existingItem = taskList[i];
                    bool stillExists = newTasks.Any(x => x.BinID == existingItem.BinID);
                    if (!stillExists) taskList.RemoveAt(i);
                }

                foreach (var newItem in newTasks)
                {
                    var existingItem = taskList.FirstOrDefault(x => x.BinID == newItem.BinID);


                    if (existingItem != null)
                    {
                        if (existingItem.CurrentQuantity != newItem.CurrentQuantity)
                        {
                            int index = taskList.IndexOf(existingItem);
                            taskList[index] = newItem;
                        }
                    }
                    else
                    {
                        taskList.Add(newItem);
                    }
                }

                txtCount.Text = $"{taskList.Count} TASKS PENDING";

                if (taskList.Count > 0)
                {
                    txtConnection.Text = "RUNNER: QUEUED";
                    txtConnection.Foreground = Brushes.Orange;

                    EmptyState.Visibility = Visibility.Collapsed;
                    TaskList.Visibility = Visibility.Visible;
                }
                else
                {
                    txtConnection.Text = "RUNNER: IDLE";
                    txtConnection.Foreground = Brushes.Gray;

                    EmptyState.Visibility = Visibility.Visible;
                    TaskList.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                txtConnection.Text = "DISCONNECTED";
                txtConnection.Foreground = Brushes.Red;
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
    }
}