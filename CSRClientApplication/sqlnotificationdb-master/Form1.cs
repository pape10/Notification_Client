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
namespace WindowsApplication17
{
    public partial class Form1 : Form
    {                
        private SqlConnection connection = null;
        private SqlCommand command = null;
        // The Service Name is required to correctly 
        // register for notification.
        // The Service Name must be already defined with
        // Service Broker for the database you are querying.
        private const string ServiceName = "ChangeNotifications";
        // Spercify how long the notification request
        // should wait before timing out.
        // This value waits for 60 minutes. 
        private int NotificationTimeout = 3600;
        public Form1()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Minimized;
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
            return @"Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=SqlNotDb;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False";
        }
        private string GetSQL()
        {
            return "SELECT Id,Name,EventDate from SQLNotificationRequestTable";            
        }
        private string GetListenerSQL()
        {
            // Note that ChangeMessages is the name
            // of the Service Broker queue that must
            // be already defined.
            return "WAITFOR ( RECEIVE * FROM ChangeMessages);";
        }
        private void StartListener()
        {
            // A seperate listener thread is needed to 
            // monitor the queue for notifications.
            Thread listener = new Thread(Listen);
            listener.Name = "Query Notification Watcher";
            listener.Start();
        }
        private void Listen()
        {
            using (SqlConnection connection =
            new SqlConnection(GetConnectionString()))
            {
                using (SqlCommand command =
                new SqlCommand(GetListenerSQL(), connection))
                {
                    connection.Open();
                    // Make sure we don't time out before the
                    // notification request times out.
                    command.CommandTimeout = NotificationTimeout + 15;
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
        private string messageText;
        private void OnNotificationComplete(object sender, EventArgs e)
        {
            messageText = messageText.Replace("??", "");
            label1.Text = messageText.Replace("\0","");
            notifyIcon1.Visible = true;
            //notifyIcon1.Icon = 
            notifyIcon1.ShowBalloonTip(1000, "Important Notice", messageText.Replace("\0", "") , ToolTipIcon.Info);
            // The user can decide to register
            // and request a new notification by
            // checking the CheckBox on the form.
            GetData();
        }
        
        private void GetData()
        {            
                // Make sure the command object does not already have
                // a notification object associated with it.
                command.Notification = null;            
                SqlNotificationRequest snr =
                new SqlNotificationRequest();
                snr.UserData = new Guid().ToString();
                snr.Options = "Service=" + ServiceName;
                // If a time-out occurs, a notification
                // will indicate that is the 
                // reason for the notification.
                snr.Timeout = NotificationTimeout;
                command.Notification = snr;                                   
                // Start the background listener.
                StartListener();            
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (connection != null)
            {
                connection.Close();
            }
        }

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
    }
}
