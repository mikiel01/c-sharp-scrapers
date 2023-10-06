using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Data.SqlClient;

namespace ConsoleApp1
{
    class Program
    {
        static int requestErrorCount = 0;
        static string server = "Email Server";
        static readonly string sql_connection = "Server=EC2AMAZ-P516DRI;Database=GasFundamentals;Trusted_Connection=Yes";
        void WriteToLog(string filepath, string message)
        {
            if (!File.Exists(filepath))
            {
                using (StreamWriter writer = File.CreateText(filepath))
                {
                    writer.WriteLine(System.DateTime.Now.ToString() + ": " + message);
                }
            }
            else
            {
                using (StreamWriter writer = File.AppendText(filepath))
                {
                    writer.WriteLine(System.DateTime.Now.ToString() + ": " + message);
                }
            }
        }

        private System.Net.CookieContainer GetCookie(string url, string logFilePath)
        {
            HttpWebRequest req;
            HttpWebResponse result;
            try
            {

                object cookies = new System.Net.CookieContainer();
                req = (HttpWebRequest)WebRequest.Create(url);
                req.CookieContainer = (System.Net.CookieContainer)cookies;
                result = (HttpWebResponse)req.GetResponse();
                req.Abort();
                return (System.Net.CookieContainer)cookies;
            }
            catch (Exception e)
            {
                requestErrorCount++;
                WriteToLog(logFilePath, e.ToString());
                Console.WriteLine("Error: {0}", e.ToString());
                if (requestErrorCount > 5)
                {
                    SendEmail(server, url, logFilePath);
                }
                return new System.Net.CookieContainer();
            }
        }
        void GetCSV(string url, string path, string logFilePath)
        {

            try
            {
                CookieContainer cookies = GetCookie(url, logFilePath);
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.CookieContainer = cookies;
                HttpWebResponse result = (HttpWebResponse)req.GetResponse();
                if (result.StatusCode == HttpStatusCode.OK)
                {                    
                    using (var strReceiveStream = result.GetResponseStream())
                    using (var fs = new FileStream(path, FileMode.Create))
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        while ((bytesRead = strReceiveStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fs.Write(buffer, 0, bytesRead);
                            bytesRead = strReceiveStream.Read(buffer, 0, buffer.Length);
                        }
                    }
                    
                }
                else
                {
                    WriteToLog(logFilePath, String.Format("Error: HttpCode Not OK for link {0}", url));
                    Console.WriteLine("ERROR");
                }
            }
            catch (Exception e)
            {
                requestErrorCount++;
                Console.WriteLine("Error: {0}", e.ToString());
                if (requestErrorCount > 5)
                {
                    SendEmail(server, url, logFilePath);
                }
                string errorLine = String.Format("Error: {0}", e.ToString());
                WriteToLog(logFilePath, errorLine);
                GetCSV(url, path, logFilePath);
            }
        }

        static string MakeDoubleDigit(int n)
        {
            if (n < 10)
            {
                return String.Format("0{0}", n.ToString());
            }
            return n.ToString();
        }

        string[] GetDateStrings(System.DateTime datetime, int days_back)
        {
            string[] dateStrings = new string[days_back + 1];
            for (int i = 0; i >= days_back * -1; --i)
            {
                System.DateTime newDateTime = datetime.AddDays(i);
                string dateString = String.Format("{0}{1}{2}", MakeDoubleDigit(newDateTime.Year), MakeDoubleDigit(newDateTime.Month), MakeDoubleDigit(newDateTime.Day));
                dateStrings[i * -1] = dateString;
            }
            return dateStrings;
        }

        System.DateTime ConvertPPTToEST(System.DateTime datetime)
        {
            if (IsDaylightSavingTime(datetime))
            {
                return datetime.AddHours(2);
            }
            return datetime.AddHours(3);
        }
        System.DateTime ConvertToEST(System.DateTime datetime)
        {
            return datetime.ToUniversalTime().AddHours(-5);
        }

        void SendEmail(string server, string url, string logFilePath)
        {
            string to = "";
            string from = "";
            MailMessage message = new MailMessage(from, to);
            message.Subject = "Error: Extended Timeout";
            message.Body = @"Error From CA ISO Scraper : Extended timeout error for url: " + url;
            SmtpClient client = new SmtpClient(server);

            client.UseDefaultCredentials = true;

            try
            {
                client.Send(message);
                WriteToLog(logFilePath, "Sent timeout error email message");
            }
            catch (Exception e)
            {
                string errorLine = String.Format("Caught exception in SendEmail: {0}", e.ToString());
                Console.WriteLine("Caught exception in SendEmail: {0}", e.ToString());
            }

        }
        Dictionary<string, double> ReadCSV(string filename, string logPathFile)
        {
            Dictionary<string, double> energyValues = new Dictionary<string, double>();
            Console.WriteLine(filename);
            using (var reader = new StreamReader(@"" + filename))
            {
                
                var gotHeaders = false;
                List<string> headers = new List<string>();
            
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');
                    if (!gotHeaders)
                    {
                        foreach (var value in values)
                        {
                            headers.Add(value);
                        }
                        gotHeaders = true;
                    }
                    else
                    {
                        for (int i = 1; i < headers.Count; ++i)
                        {
                            string key = String.Format("{0}_{1}", values[0], headers[i]);
                            if (values.Length > i && Int32.TryParse(values[i], out int value) )
                            {   
                                energyValues[key] = value;
                            }
                            else
                            {
                                var errorMessage = String.Format("Error with {0}: ", key);
                                WriteToLog(logPathFile, errorMessage);
                            }
                        }

                    }
                }
            }
            return energyValues;
        }

        static string DateTimeToString(System.DateTime d)
        {
            var str = String.Format("{0}-{1}-{2} {3}:{4}:{5}", d.Year, MakeDoubleDigit(d.Month), MakeDoubleDigit(d.Day), MakeDoubleDigit(d.Hour), MakeDoubleDigit(d.Minute), MakeDoubleDigit(d.Second));
            return str;
        }
        static string DateTimeToFileString(System.DateTime d)
        {
            var str = String.Format("{0}{1}{2}_{3}{4}{5}", d.Year, MakeDoubleDigit(d.Month), MakeDoubleDigit(d.Day), MakeDoubleDigit(d.Hour), MakeDoubleDigit(d.Minute), MakeDoubleDigit(d.Second));
            return str;
        }

        System.DateTime StringToDateTime(string datetimeString, string logFilePath)
        {
            int year = 0;
            int month = 0;
            int day = 0;
            Boolean errorMessage = false;
            if (Int32.TryParse(datetimeString.Substring(0, 4), out int n))
            {
                year = n;
            }
            else
            {
                Console.WriteLine("String has no valid Year");
                errorMessage = true;
            }
            if (Int32.TryParse(datetimeString.Substring(4, 2), out int n2))
            {
                month = n2;
            }
            else
            {
                Console.WriteLine("String has no valid Month");
                errorMessage = true;
            }
            if (Int32.TryParse(datetimeString.Substring(6, 2), out int n3))
            {
                day = n3;
            }
            else
            {
                Console.WriteLine("String has no valid Day");
                errorMessage = true;
            }
            if (errorMessage)
            {
                WriteToLog(logFilePath, "Invalid String from Date");
            }
            return new System.DateTime(year, month, day, 0, 0, 0);
        }

        int GetHour(string time, string logFilePath)
        {
            string[] times = time.Split(':');
            int hour = -1;
            if (Int32.TryParse(times[0], out int n))
            {
                hour = n;
            }
            else
            {
                Console.WriteLine("String has no valid Hour");
                WriteToLog(logFilePath, "String has no valid Hour");
            }

            return hour;
        }

        int GetMinute(string time, string logFilePath)
        {
            string[] times = time.Split(':');
            int minute = -1;
            if (Int32.TryParse(times[1], out int n))
            {
                minute = n;
            }
            else
            {
                Console.WriteLine("String has no valid Minute");
                WriteToLog(logFilePath, "String has no valid Minute");
                minute = -1;
            }
            return minute;
        }

        string GetMostRecentFile(string path, string filename)
        {
            string[] files = Directory.GetFiles(path, String.Format("*{0}*", filename));
            Array.Sort(files);
            try
            {
                if (files.Length > 1)
                {
                    return files[files.Length - 2];
                }
                
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e.ToString());
            }
            return "";
        }

        Boolean IsDaylightSavingTime(System.DateTime datetime)
        {
            string cityTz = "Eastern Standard Time";
            TimeZoneInfo tzi = TimeZoneInfo.FindSystemTimeZoneById(cityTz);
            Boolean isDaylight = tzi.IsDaylightSavingTime(datetime);
            return isDaylight;
        }

        Dictionary<string, double> CompareCSVDictionaries(string date, Dictionary<string, double> newCSV, Dictionary<string, double> originalCSV, string newCSVFilename, string logFilePath)
        {
            var itemsToUpdate = new Dictionary<string, double>();
            foreach (var key in newCSV.Keys)
            {
             
                if (originalCSV.ContainsKey(key) && originalCSV[key] != newCSV[key])
                {

                    itemsToUpdate[key] = newCSV[key];
                    UpdateRecord(key, newCSV[key], date, newCSVFilename, logFilePath);
                }
                else if (!originalCSV.ContainsKey(key))
                {
                    AddRecord(key, newCSV[key], date, newCSVFilename, logFilePath);
                }

            }

            return itemsToUpdate;
        }


        
        void ExecuteSQLStatement(SqlConnection sqlConnection, string query, string logFilePath)
        {
            try
            {
                using (var connection = sqlConnection)
                {
                    SqlCommand cmd = new SqlCommand(query, connection);

                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandText = query;
                    cmd.Connection = connection;

                    cmd.Connection.Open();
                    cmd.ExecuteNonQuery();
                    cmd.Connection.Close();
                    string executionMessage = String.Format("Executed Query: {0}", query);
                    WriteToLog(logFilePath, executionMessage);
                }
            }
            catch (Exception e)
            {
                var errorMessage = String.Format("Error executing query: {0} , Error Message: {1}", query, e.ToString());
                WriteToLog(logFilePath, errorMessage);
                Console.WriteLine(e.ToString());
            }
        }
        
        void UpdateRecord(string key, double value, string date, string filename, string logFilePath)
        {
            string[] records = key.Split('_');
            var time = records[0];
            var fuelType = records[1];
            var fileDate = date;
            System.DateTime datetime = StringToDateTime(date, logFilePath);
            datetime.AddHours(GetHour(time, logFilePath));
            datetime.AddMinutes(GetMinute(time, logFilePath));
            var rowEST = ConvertPPTToEST(datetime);
            var rowESTString = DateTimeToString(rowEST);
            System.DateTime estNow = ConvertToEST(System.DateTime.Now);
            SqlConnection connection = new SqlConnection(sql_connection);
            
            string sqlStatement = String.Format("UPDATE fuelsource SET VALUE = {0}, LastUpdateEST = '{1}', Filename = '{2}' WHERE RowDateEST LIKE '{3}' AND FuelType LIKE '{4}'", value.ToString(), estNow.ToString(), filename, rowESTString, fuelType);
            ExecuteSQLStatement(connection, sqlStatement, logFilePath);
        }

        void AddRecord(string key, double value, string date, string filename, string logFilePath)
        {
            string[] records = key.Split('_');
            var time = records[0];
            var fuelType = records[1];
            System.DateTime datetime = StringToDateTime(date, logFilePath);
            datetime = datetime.AddHours(GetHour(time, logFilePath));
            datetime = datetime.AddMinutes(GetMinute(time, logFilePath));

            var rowEST = ConvertPPTToEST(datetime);
            var rowESTString = DateTimeToString(rowEST);
            System.DateTime estNow = ConvertToEST(System.DateTime.Now);
            System.DateTime estDatetime = StringToDateTime(date, logFilePath);
            SqlConnection connection = new SqlConnection(sql_connection);
            
            
            var sqlStatement = String.Format("INSERT INTO fuelsource (RowDateEST, FuelType, Value, CreationDateEST, LastUpdateEST, Filename) VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}')", rowESTString, fuelType, value, estNow.ToString(), estNow, filename);
            ExecuteSQLStatement(connection, sqlStatement, logFilePath);
        }

        string DateTimeToLogFilename(System.DateTime datetime)
        {
            return String.Format("{0}{1}{2}", datetime.Year.ToString(), MakeDoubleDigit(datetime.Month), MakeDoubleDigit(datetime.Day));
        }

        System.DateTime GetCurrentPPT()
        {
            if (IsDaylightSavingTime(System.DateTime.UtcNow.AddHours(-7)))
            {
                return System.DateTime.UtcNow.AddHours(-7);
            }
            return System.DateTime.UtcNow.AddHours(-8);
        }
        void Run()
        {
            int days_back = 5;
            string url_prefix = "http://www.caiso.com/outlook/SP/History/";
            string gasFundamentalsPath = "C:/Project/GasFundamentals/";
            Boolean hasDownloaded = false;
            var current_datetime = GetCurrentPPT();
            System.DateTime last_datetime = current_datetime.AddDays(-1);
            string logFilePathPrefix = "C:/Project/GasFundamentals/";
            
            while (true)
            {
                current_datetime = GetCurrentPPT();

                if (current_datetime.Minute % 15 == 9)
                {
                    
                    if (last_datetime.Day != current_datetime.Day)
                    {
                        
                        string logFilePath = String.Format("{0}{1}", logFilePathPrefix, DateTimeToLogFilename(current_datetime));
                        WriteToLog(logFilePath, "Started New Day");
                        last_datetime = current_datetime;
                    }
                    if (!hasDownloaded)
                    {
                        string logFilePath = String.Format("{0}{1}", logFilePathPrefix, DateTimeToLogFilename(current_datetime));
                        Console.WriteLine("Starting");
                        var year = current_datetime.Year.ToString();
                        var month = MakeDoubleDigit(current_datetime.Month);
                        System.IO.Directory.CreateDirectory(String.Format("{0}{1}/{2}/", gasFundamentalsPath, year, month));
                        string[] dateStrings = GetDateStrings(current_datetime, days_back);
                        foreach (var datestring in dateStrings)
                        {
                            Console.WriteLine(datestring);
                            string file_save_path = String.Format("{0}{1}/{2}/", gasFundamentalsPath, year, month);
                            System.IO.Directory.CreateDirectory(file_save_path);
                            string current_datetime_string = DateTimeToString(current_datetime);
                            string filename = String.Format("fuelsource_{0}_{1}.csv", datestring, DateTimeToFileString(current_datetime));
                            string url = String.Format("{0}{1}/fuelsource.csv", url_prefix, datestring);
                            string file_save_path_and_name = String.Format("{0}{1}", file_save_path, filename);
                            Console.WriteLine(url);
                            try
                            {
                                GetCSV(url, file_save_path_and_name, logFilePath);
                                var csvData = ReadCSV(file_save_path_and_name, logFilePath);
                                string dateStringYear = datestring.Substring(0, 4);
                                string dateStringMonth = datestring.Substring(4, 2);
                                string dateStringDay = datestring.Substring(6, 2);
                                string oldFilePath = String.Format("{0}{1}/{2}/", gasFundamentalsPath, dateStringYear, dateStringMonth);
                                var mostRecentFile = GetMostRecentFile(oldFilePath, String.Format("*_{0}_*", datestring));
                                var csvData2 = new Dictionary<string, double>();
                                Console.WriteLine("RECENT FILE: " + mostRecentFile);
                                if (!mostRecentFile.Equals(""))
                                {
                                    csvData2 = ReadCSV(mostRecentFile, logFilePath);
                                }
                              
                                    CompareCSVDictionaries(datestring, csvData, csvData2, file_save_path_and_name, logFilePath);

                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Error: {0}", e.ToString());
                                var errorMessage = String.Format("Error: {0}", e.ToString());
                                WriteToLog(logFilePath, errorMessage);
                            }



                        }
                        hasDownloaded = true;
                    }

                }
                else
                {
                    hasDownloaded = false;
                }

            }
        }
        public static void Main(String[] args)
        {
            Program program = new Program();
            program.Run();
        }
    
    }


}


