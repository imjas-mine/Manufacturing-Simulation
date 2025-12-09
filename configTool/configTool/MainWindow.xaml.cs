/************************************************************************************
 * File: MainWindow.xaml.cs
 * Project: MileStone 1
 * Description: This WPF application provides a configuration management tool that connects 
 *              to a SQL Server database. It loads configuration data, allows editing of 
 *              configuration values, and saves updates back to the database.
 * 
 * Author: Jasmine Kaur
 * Last Modified: 31 Oct 2025
 ************************************************************************************/

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace configTool
{
    
    public partial class MainWindow : Window
    {
        private string connectionString;
        private DataTable configTable;


        /* Function Name: MainWindow
         * Description: Initializes the main window, loads configuration settings, and validates the database connection string.
         */
        public MainWindow()
        {
            InitializeComponent();
            try
            {

                connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["FogLampManufacturing"].ConnectionString;
                if (string.IsNullOrEmpty(connectionString))
                {
                    MessageBox.Show("Connection string 'FogLampManufacturing' not found in App.config.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                    return;
                }

                LoadConfiguration();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error Loading Configuration: {ex.Message}", "Load Configuration Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        /* Function Name: LoadConfiguration
         * Description: Loads configuration data from the database into a DataTable and binds it to the DataGrid control for display and editing.
         */
        private void LoadConfiguration()
        {
            try
            {

                configTable = new DataTable();
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    SqlDataAdapter adapter = new SqlDataAdapter("SELECT * FROM Configuration", conn);
                    adapter.Fill(configTable);
                }
                ConfigDataGrid.ItemsSource = configTable.DefaultView;
            }
            catch (SqlException ex)
            {
                MessageBox.Show(
                    $"Database connection error:\n\n{ex.Message}\n\nPlease verify your connection string and database availability.",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(
                    $"Invalid connection configuration:\n\n{ex.Message}",
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unexpected error:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

       /* Function Name: SaveChanges_Click
        * Description: Saves any modified configuration records from the DataGrid back to the database.
        */
        private void SaveChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    foreach (DataRow row in configTable.Rows)
                    {
                        if (row.RowState == DataRowState.Modified)
                        {
                            string desc = row["ConfigDescription"].ToString();
                            string value = row["Value"].ToString();

                            string sql = "UPDATE Configuration SET Value = @value WHERE ConfigDescription = @desc";
                            using (SqlCommand cmd = new SqlCommand(sql, conn))
                            {
                                cmd.Parameters.AddWithValue("@value", value);
                                cmd.Parameters.AddWithValue("@desc", desc);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }

                MessageBox.Show("Configuration saved successfully!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                configTable.AcceptChanges();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving changes: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
    }
}
