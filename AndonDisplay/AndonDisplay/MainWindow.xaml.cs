/*
 * File Name: MainWindow.xaml.cs
 * Author: Jasmine Kaur
 * Date: 2025-12-04
 * Description: This file contains the logic for the Workstation Andon Display.
 * It polls the database every 2 seconds to show real-time stock levels
 * and production metrics for a specific station.
 */

using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Protocols;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace AndonDisplay
{
    public partial class MainWindow : Window
    {
        private readonly string connectionString;
        private int stationId = 1; 
        private readonly DispatcherTimer timer;

        /*-----------------------------------------------------------
         * Function Name: MainWindow (Constructor)
         * Description:   Initializes the window, loads the connection string,
         * sets up the timer for data refreshing, and performs the first load.
         * Parameters:    None
         * Returns:       None
         *-----------------------------------------------------------*/
        public MainWindow()
        {
            try
            {
                InitializeComponent();

                connectionString = ConfigurationManager.ConnectionStrings["FogLampDB"]?.ConnectionString;

                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(2);
                timer.Tick += Timer_Tick; 
                timer.Start();

                RefreshData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup Error: " + ex.ToString());
            }
        }

        /*-----------------------------------------------------------
         * Function Name: BtnLoad_Click
         * Description:   Event handler for the "Load" button. It reads the
         * Station ID from the text box and refreshes the display.
         * Parameters:    sender (object), e (RoutedEventArgs)
         * Returns:       None
         *-----------------------------------------------------------*/
        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtStationId.Text, out int newId))
            {
                stationId = newId;
                txtStationTitle.Text = $"STATION {stationId}";
                RefreshData();
            }
            else
            {
                MessageBox.Show("Please enter a valid number.");
            }
        }

        /*-----------------------------------------------------------
         * Function Name: Timer_Tick
         * Description:   Event handler called by the timer every 2 seconds.
         * It triggers the RefreshData function to update the screen.
         * Parameters:    sender (object), e (EventArgs)
         * Returns:       None
         *-----------------------------------------------------------*/
        private void Timer_Tick(object sender, EventArgs e)
        {
            RefreshData();
        }

        /*-----------------------------------------------------------
         * Function Name: RefreshData
         * Description:   Connects to the database and calls helper functions
         * to update the header (metrics) and the bin list (inventory).
         * Handles connection errors gracefully.
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

                    UpdateHeader(conn);
                    UpdateBins(conn);
                    
                    txtConnection.Text = "● CONNECTED";
                    txtConnection.Foreground = Brushes.LimeGreen;
                }
            }
            catch (Exception ex)
            {
                txtConnection.Text = "● DISCONNECTED";
                txtConnection.Foreground = Brushes.Red;
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        /*-----------------------------------------------------------
         * Function Name: UpdateHeader
         * Description:   Queries 'vw_StationStatus' to get the Station Name,
         * Produced Count, and In-Progress Count.
         * Parameters:    conn (SqlConnection) - Open database connection
         * Returns:       None
         *-----------------------------------------------------------*/
        private void UpdateHeader(SqlConnection conn)
        {
            string sql = "SELECT StationName, InProgress, Produced FROM dbo.vw_StationStatus WHERE StationID = @ID";

            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ID", stationId);

                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    if (rdr.Read())
                    {
                        txtStationTitle.Text = rdr["StationName"].ToString();
                        txtProduced.Text = $"PRODUCED: {rdr["Produced"]}";
                        txtInProgress.Text = $"ASSEMBLING: {rdr["InProgress"]}";
                    }
                    else
                    {
                        txtStationTitle.Text = $"STATION {stationId} (OFFLINE)";
                    }
                }
            }
        }

        /*-----------------------------------------------------------
         * Function Name: UpdateBins
         * Description:   Queries 'vw_BinStatus' to get the inventory levels
         * for every bin at this station. It binds the result to the
         * ItemsControl to generate the cards dynamically.
         * Parameters:    conn (SqlConnection) - Open database connection
         * Returns:       None
         *-----------------------------------------------------------*/
        private void UpdateBins(SqlConnection conn)
        {
            var binList = new List<BinItem>();

            string sql = "SELECT PartName, CurrentQuantity, DefaultCapacity, IsLow FROM dbo.vw_BinStatus WHERE StationID = @ID";

            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@ID", stationId);

                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        binList.Add(new BinItem
                        {
                            PartName = rdr["PartName"].ToString().ToUpper(),
                            CurrentQuantity = Convert.ToInt32(rdr["CurrentQuantity"]),
                            MaxCapacity = Convert.ToInt32(rdr["DefaultCapacity"]),
                            IsLow = Convert.ToInt32(rdr["IsLow"]) == 1
                        });
                    }
                }
            }

            BinList.ItemsSource = binList;
        }
    }
}