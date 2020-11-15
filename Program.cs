using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ModularAM
{
    public static class Program
    {
        private const string STR_CONNECTION = @"Server=tcp:modularam.database.windows.net,1433;Initial Catalog=mamDb;Persist Security Info=False;User ID=aluxious;Password=Qaz1wsx2@;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
        private const string STR_URL = @"https://www.ccilindia.com/FPI_ARCV.aspx";

        [Obsolete]
        static void Main(string[] args)
        {
            try
            {
                CleanupProcesses();

                Console.WriteLine("Connecting to database...");
                // /* TODO: Make connection string configurable */
                //string data_source = ConfigurationManager.AppSettings["data_source"];
                //string port = ConfigurationManager.AppSettings["port"];
                //string database_name = ConfigurationManager.AppSettings["database_name"];
                //string user_name = ConfigurationManager.AppSettings["user_name"];
                //string password = ConfigurationManager.AppSettings["password"];
                //string connectionString = @"Data Source=" + data_source + "," + port + ";Initial Catalog=" + database_name
                //    + ";User Id=" + user_name + ";Password=" + password;
                int totalStored = GetTotalStored();

                WebScrape(totalStored);
            }
            catch (Exception exxx)
            {
                Console.WriteLine(exxx.Message);
            }
        }

        [Obsolete]
        private static void WebScrape(int totalStored)
        {
            Console.WriteLine("Opening Chrome...");
            ChromeDriverService chromeDriverService = null;
            ChromeDriver driver = null;
            try
            {
                chromeDriverService = ChromeDriverService.CreateDefaultService(Directory.GetCurrentDirectory());
                chromeDriverService.HideCommandPromptWindow = true;
                var options = new ChromeOptions();
                options.AddArgument("--disable-notifications");
                options.AddArgument("--no-sandbox");
                options.AddArguments("headless");

                driver = new ChromeDriver(chromeDriverService, options);
                driver.Navigate().GoToUrl(STR_URL);
                Console.WriteLine("Website loaded. Start crawling data...");
                int totalDateWeb = new SelectElement(driver.FindElementById("drpArchival")).Options.Count;
                Console.WriteLine("Total data available (dates): " + totalDateWeb);

                int rowInserted = 0;

                using (SqlConnection con = new SqlConnection(STR_CONNECTION))
                {
                    for (int i = totalDateWeb - totalStored - 1; i > 0; i--)
                    {
                        string date = new SelectElement(driver.FindElementById("drpArchival")).Options[i].Text.Trim();
                        // Crawl data to insert database
                        Console.WriteLine("Crawling data of date: " + date);
                        var tmpSelect = new SelectElement(driver.FindElementById("drpArchival"));
                        tmpSelect.SelectByText(date);
                        for (int pages = 2; ; pages++)
                        {
                            IWebElement table = FindElementEx(driver, By.Id("grdFPISWH"), 5);
                            if (table == null)
                                Console.WriteLine("Table data not found");
                            else
                            {
                                var rows = table.FindElements(By.TagName("tr"));

                                for (int r = 1; r < rows.Count; r++)
                                {
                                    var rowTds = rows[r].FindElements(By.TagName("td"));
                                    if (rowTds.Count == 5 && !rowTds[0].Text.Trim().Equals(""))
                                    {

                                        string saveData = "INSERT into secwise_holdings (cutoff_date, isin, security_description, indicative_value, outstanding_position, sec_holdings, crawled_time)" +
                                            " VALUES (@cutoff_date, @isin, @security_description, @indicative_value, @outstanding_position, @sec_holdings, @crawled_time)";

                                        using (SqlCommand command = new SqlCommand(saveData, con))
                                        {
                                            command.Parameters.AddWithValue("@cutoff_date", date);
                                            command.Parameters.AddWithValue("@isin", rowTds[0].Text.Trim());
                                            command.Parameters.AddWithValue("@security_description", rowTds[1].Text.Trim());
                                            command.Parameters.AddWithValue("@indicative_value", rowTds[2].Text.Trim());
                                            command.Parameters.AddWithValue("@outstanding_position", rowTds[3].Text.Trim());
                                            command.Parameters.AddWithValue("@sec_holdings", rowTds[4].Text.Trim());
                                            command.Parameters.AddWithValue("@crawled_time", DateTime.Now);
                                            try
                                            {
                                                con.Open();
                                                int recordsAffected = command.ExecuteNonQuery();
#if DEBUG
                                                Console.WriteLine("Inserted into database " + rowTds[0].Text.Trim());
#endif
                                            }
                                            catch (SqlException ex)
                                            {
                                                // error here
                                                Console.WriteLine(ex.Message);
                                            }
                                            finally
                                            {
                                                con.Close();
                                            }
                                        }
                                        rowInserted++;
                                    }
                                }
                            }
                            // Load data from other pages
                            try
                            {
                                var link = driver.FindElementByLinkText(pages.ToString());
                                link.Click();
                            }
                            catch (NoSuchElementException)
                            {
                                // link does not exist
                                break;
                            }
                        }
                    }
                }
                Console.WriteLine("Total rows inserted: " + rowInserted);
            }
            catch (Exception ex)
            {
                // TODO: log
                throw ex;
            }
            finally
            {
                if (driver != null)
                    driver.Close();
                if (chromeDriverService != null)
                    chromeDriverService.Dispose();
            }
        }

        private static int GetTotalStored()
        {
            int totalStored = 0;
            using (SqlConnection con = new SqlConnection(STR_CONNECTION))
            {
                con.Open();
                // Delete latest date stored to recrawl (for update purposes)
                string strDelete = "delete from secwise_holdings where cutoff_date = (SELECT top 1 cutoff_date from secwise_holdings order by crawled_time desc)";
                using (SqlCommand deleteCmd = new SqlCommand(strDelete, con))
                {
                    int recordsAffected = deleteCmd.ExecuteNonQuery();
                }

                //Get total stored
                string strSelect = "select COUNT(DISTINCT cutoff_date) AS Count from secwise_holdings";
                using (SqlCommand command = new SqlCommand(strSelect, con))
                {
                    command.Connection = con;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            totalStored = reader.GetInt32(0);
                        }
                    }
                }
            }
            Console.WriteLine("Total stored: " + totalStored);
            return totalStored;
        }

        private static void CleanupProcesses()
        {
            foreach (var process in Process.GetProcessesByName("chromedriver"))
            {
                process.Kill();
            }
            foreach (var process in Process.GetProcessesByName("chrome"))
            {
                process.Kill();
            }
        }

        [Obsolete]
        static IWebElement FindElementEx(this IWebDriver driver, By by, int timeoutInSeconds)
        {
            try
            {
                if (timeoutInSeconds > 0)
                {
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
                    wait.Until(ExpectedConditions.ElementIsVisible(by));
                    return driver.FindElement(by);
                }
                return driver.FindElement(by);
            }
            catch
            {
                return null;
            }
        }
    }
}
