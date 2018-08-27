using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Threading;
using System.Resources;
using System.Reflection;

namespace CSRClientApp
{
    public partial class CSRClient : Form
    {
        
        int number;
        static string message = "";
        string[] act_parts_act = new string[7];
        static string username = GetUserName();
        private SqlConnection connection = null;
        private SqlCommand command = null;
        // The Service Name is required to correctly 
        // register for notification.
        // The Service Name must be already defined with
        // Service Broker for the database you are querying.
        private const string ServiceName = "ChangeNotifications";
        // Spercify how long the notification request
        // should wait before timing out.
        // This value waits for 10 minutes. 
        private int NotificationTimeout = 0;
        public CSRClient()
        {
            InitializeComponent();
           
            this.Hide();
            this.WindowState = FormWindowState.Minimized;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.ShowInTaskbar = false;
            
            notifyIcon1.Visible = false;
            
        }
        private string GetConnectionString()
        {
            // To avoid storing the connection string in your code,
            // you can retrive it from a configuration file.
            // In general, client applications don't need to incur the
            // overhead of connection pooling.
            //return "Data Source=localhost;Integrated Security=SSPI;" +
            //"Initial Catalog=SQLNotificationRequestDB;Pooling=False;";
            return @"Data Source=W101R1SPF2\SQLEXPRESS;Initial Catalog=windowsnot24;Integrated Security=True; ";
        }
        private string GetSQL()
        {
            return "SELECT EventID,EventName,EventDate,EventMessage,EventURL,PopupDuration from Event";            
        }
        private string GetListenerSQL()
        {
            return @"
DECLARE @NotificationDialog uniqueidentifier
SET QUOTED_IDENTIFIER ON
BEGIN DIALOG CONVERSATION @NotificationDialog
FROM SERVICE ChangeNotifications
TO SERVICE 'ChangeNotifications'
ON CONTRACT [http://schemas.microsoft.com/SQL/Notifications/PostQueryNotification]
WITH ENCRYPTION = OFF;	
delete from Conversations where NotificationHandle = '" + guid + @"'
delete from Conversations where UserName = '" + username + @"'
insert into Conversations(ConversationHandle,NotificationHandle,UserName) values (@NotificationDialog,'" + guid + @"','"+username+@"')
WAITFOR ( RECEIVE * FROM ChangeMessages);";
        }


        //this starts the Listen Thread


        private void StartListener()
        {
            // A seperate listener thread is needed to 
            // monitor the queue for notifications.
            Thread listener = new Thread(Listen);
            listener.Name = "Query Notification Watcher";
            listener.Start();
        }

        //this is creates a listener method


        private void Listen()
        {
            try
            {
                using (SqlConnection connection =
       new SqlConnection(GetConnectionString()))
                {
                    using (SqlCommand command =
                    new SqlCommand(GetListenerSQL(), connection))
                    {
                        connection.Open();
                        //MessageBox.Show("inside try inside connection");
                        // Make sure we don't time out before the
                        // notification request times out.
                        command.CommandTimeout = NotificationTimeout;
                        SqlDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            messageText = System.Text.ASCIIEncoding.ASCII.GetString((byte[])reader.GetValue(13)).ToString();
                            // Empty queue of messages.
                            // Application logic could parse
                            // the queue data and 
                            // change its notification logic.
                        }

                        object[] args = { this, EventArgs.Empty };
                        EventHandler notify =
                        new EventHandler(OnNotificationComplete);
                        // Notify the UI thread that a notification
                        // has occurred.
                        this.BeginInvoke(notify, args);
                    }
                }
            }
            catch(SqlException e)
            {
                //MessageBox.Show("exception thrown");
                //MessageBox.Show("connection not got");
                //it is in miliseconds
                Thread.Sleep(3000);

                StartListener();
            }
        }
        private string messageText;

        //this method handles the functionality that needs to happen when a new message is recieved .

        private void OnNotificationComplete(object sender, EventArgs e)
        {
            messageText = messageText.Replace("??", "");
            string finalMessageText = messageText.Replace("\0", "");
            string[] cols = GetIndCols(finalMessageText);
            
            act_parts_act = cols;
            notifyIcon1.Visible = true;
            
            notifyIcon1.ShowBalloonTip(Int32.Parse(cols[5]),cols[1],cols[2]+" "+cols[3], ToolTipIcon.Info);
            
            GetData();
        }

        //this message sepparates the message separated by deliminator { } and stores it an string array
        private string[] GetIndCols(string s)
        {
            string[] parts = s.Split('{');
            string[] act_parts = new string[parts.Length];
            for(int i=1;i<parts.Length;i++)
            {
                parts[i] = parts[i].Remove(parts[i].Length - 1);
                act_parts[i - 1] = parts[i];
            }
            //MessageBox.Show(""+act_parts.Length);
            return act_parts;
        }

        //construct a unique guid that handles the conversation table
        private string guid = Guid.NewGuid().ToString();

        private void GetData()
        {
            // Make sure the command object does not already have
            // a notification object associated with it.
            command.Notification = null;
            SqlNotificationRequest snr =
            new SqlNotificationRequest();
            snr.UserData = guid;
            snr.Options = "Service=" + ServiceName;
            // If a time-out occurs, a notification
            // will indicate that is the 
            // reason for the notification.

            //the timeout is set to 0 which means Tieout time is infinity
            snr.Timeout = NotificationTimeout;
            command.Notification = snr;
            // Start the background listener.
            StartListener();
        }


        //this method closes the connection when the form is closed
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (connection != null)
            {
                connection.Close();
            }
        }


        //this method handles the functionality that needs to happen when the form loads
        private void Form1_Load(object sender, EventArgs e)
        {
            
            if (connection == null)
            {
                connection = new SqlConnection(GetConnectionString());
            }
            if (command == null)
            {
                // GetSQL is a local procedure SQL string. 
                // You might want to use a stored procedure 
                // in your application.
                command = new SqlCommand(GetSQL(), connection);
            }

            GetData();
        }
        

        //this gets the username from the system
        public static string GetUserName()
        {
            string s = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            int ind = 0;
            foreach (char s1 in s)
            {
                if (s1 == '\\')
                {
                    ind++;
                    break;
                }
                ind++;
            }
            string f = "";
            for (int i = ind; i < s.Length; i++)
            {
                f = f + s[i];
            }
            return f;
        }


        //this method handles the clicking of the notifyIcon 
        private void On_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(act_parts_act[4]);
            SqlConnection conn1 = new SqlConnection(GetConnectionString());
            conn1.Open();
            SqlCommand cmd = new SqlCommand();
            string saveStaff = "INSERT into Viewed (UserName,Number) " + " VALUES ('" + username + "', '" + number + "');";
            cmd = new SqlCommand(saveStaff, conn1);
            int n = cmd.ExecuteNonQuery();

            conn1.Close();
        }

    }
}
