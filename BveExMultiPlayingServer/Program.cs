using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BveExMultiPlayingPluginServer
{
    public class ServerForm : Form
    {
        //UI類
        private Button startButton;
        private Button stopButton;
        private TextBox logTextBox;
        //DDNSインターネット通信用
        private HttpListener listener;
        private Thread listenerThread;
        //旧コード
        //private Dictionary<string, List<LocationSpeedData>> clientData;
        //private Dictionary<string, List<LocationSpeedData>> trainStatus;
        private Dictionary<string, TrainInfo> clientData;
        private readonly object lockObj = new object();

        //UIデザイン
        public ServerForm()
        {
            this.Text = "BVE MultiPlay Server";
            this.Size = new System.Drawing.Size(600, 450);

            startButton = new Button { Text = "サーバー起動", Location = new System.Drawing.Point(10, 10), Size = new System.Drawing.Size(100, 20) };
            stopButton = new Button { Text = "サーバー停止", Location = new System.Drawing.Point(120, 10), Size = new System.Drawing.Size(100, 20), Enabled = false };
            logTextBox = new TextBox { Multiline = true, ReadOnly = true, Location = new System.Drawing.Point(10, 50), Size = new System.Drawing.Size(560, 350) };

            startButton.Click += StartServer;
            stopButton.Click += StopServer;

            this.Controls.Add(startButton);
            this.Controls.Add(stopButton);
            this.Controls.Add(logTextBox);
        }

        //サーバー開始イベント時の処理
        private void StartServer(object sender, EventArgs e)
        {
            //clientData = new Dictionary<string, List<LocationSpeedData>>();
            clientData = new Dictionary<string, TrainInfo>();
            listener = new HttpListener();
            listener.Prefixes.Add("http://+:5001/api/update/");
            listener.Start();
            listenerThread = new Thread(ListenForClients) { IsBackground = true };
            listenerThread.Start();

            Log("サーバーが起動しました。" + Environment.NewLine);
            startButton.Enabled = false;
            stopButton.Enabled = true;
        }

        //サーバー停止時の処理
        private void StopServer(object sender, EventArgs e)
        {
            listener?.Stop();
            listenerThread?.Abort();
            listener = null;
            listenerThread = null;
            lock (lockObj)
            {
                clientData?.Clear();
                clientData = null;
            }

            Log("サーバーを停止しました。" + Environment.NewLine);
            startButton.Enabled = true;
            stopButton.Enabled = false;
        }

        private void ListenForClients()
        {
            while (listener.IsListening)
            {
                try
                {
                    var context = listener.GetContext();
                    Task.Run(() => HandleRequest(context));
                }
                catch (Exception ex)
                {
                    Log($"サーバーエラー: {ex.Message}" + Environment.NewLine);
                }
            }
        }
        //各クライアントからの受信データ処理
        private async Task HandleRequest(HttpListenerContext context)
        {
            if (context.Request.HttpMethod == "POST")
            {
                try 
                {
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        string requestData = await reader.ReadToEndAsync();
                        var receivedData = JsonSerializer.Deserialize<TrainInfo>(requestData);
                        if (receivedData != null)
                        {
                            lock (lockObj)
                            {
                                clientData[receivedData.ClientId] = receivedData;
                            }
                            Log($"受信: {receivedData.ClientId} " +
                                $"- {receivedData.TrainNumber} " +

                                $"- {clientData[receivedData.ClientId]}" + Environment.NewLine +
                                $"- 位置: {receivedData.Location:F1}" +
                                $", 速度: {receivedData.Speed:F1}" +
                                $", 列車長: {receivedData.Length}" + Environment.NewLine);
                        }
                    }

                    byte[] responseBytes = Encoding.UTF8.GetBytes("Data Received");
                    context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                }
                catch (Exception ex)
                {
                    Log($"受信エラー: {ex.Message}" + Environment.NewLine);
                }
                finally
                {
                    context.Response.OutputStream.Close();
                }
            }
            else if (context.Request.HttpMethod == "GET")
            {
                try
                {
                    List<TrainInfo> allData;
                    lock (lockObj)
                    {
                        allData = new List<TrainInfo>();
                        foreach (var entry in clientData)
                        {
                            allData.Add(new TrainInfo { ClientId = entry.Key, TrainNumber = entry.Value.TrainNumber, Location = entry.Value.Location, Speed = entry.Value.Speed, Length = entry.Value.Length });
                        }
                    }

                    string jsonData = JsonSerializer.Serialize(allData);
                    byte[] responseBytes = Encoding.UTF8.GetBytes(jsonData);
                    context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                }
                catch (Exception ex)
                {
                    Log($"送信エラー: {ex.Message}" + Environment.NewLine);
                }
                finally
                {
                    context.Response.OutputStream.Close();
                }
            }

            //旧コード
            /*if (context.Request.HttpMethod == "POST")
            {
                try
                {
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        string requestData = await reader.ReadToEndAsync();
                        var receivedData = JsonSerializer.Deserialize<ClientData>(requestData);
                        if (receivedData != null)
                        {
                            lock (lockObj)
                            {
                                clientData[receivedData.ClientId] = receivedData.Status;
                            }
                            Log($"受信: {receivedData.ClientId} " +
                                $"- {receivedData.Status[0].TrainNumber} " +
                                $"- {receivedData.Status[1].Data.Count} フレーム" + Environment.NewLine +
                                $"- {clientData[receivedData.ClientId]}" + Environment.NewLine +
                                $"- 位置: {receivedData.Status[1].Data[receivedData.Status[1].Data.Count - 1].Location}" +
                                $", 速度: {receivedData.Status[1].Data[receivedData.Status[1].Data.Count - 1].Speed}" + Environment.NewLine);
                        }
                    }

                    byte[] responseBytes = Encoding.UTF8.GetBytes("Data Received");
                    context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                }
                catch (Exception ex)
                {
                    Log($"受信エラー: {ex.Message}" + Environment.NewLine);
                }
                finally
                {
                    context.Response.OutputStream.Close();
                }
            }
            else if (context.Request.HttpMethod == "GET")
            {
                try
                {
                    List<ClientData> allData;
                    lock (lockObj)
                    {
                        allData = new List<ClientData>();
                        foreach (var entry in clientData)
                        {
                            allData.Add(new ClientData { ClientId = entry.Key, Status = entry.Value });
                        }
                    }

                    string jsonData = JsonSerializer.Serialize(allData);
                    byte[] responseBytes = Encoding.UTF8.GetBytes(jsonData);
                    context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                }
                catch (Exception ex)
                {
                    Log($"送信エラー: {ex.Message}" + Environment.NewLine);
                }
                finally
                {
                    context.Response.OutputStream.Close();
                }
            }*/
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => logTextBox.AppendText(message + "\n")));
            }
            else
            {
                logTextBox.AppendText(message + "\n");
            }
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ServerForm());
        }
    }

    //各列車インスタンス用の情報クラス
    public class TrainInfo
    {
        //フィールド
        //UserID
        private string clientId = "";
        //列車番号
        private string trainNumber = "";
        //位置
        private double location = 0;
        //速度
        private double speed = 0;
        //列車長
        private int length = 0;

        //情報の設定
        public void SetInfo(string clientId, string trainNumber, double location, double speed)
        {
            this.clientId = clientId;
            this.trainNumber = trainNumber;
            this.location = location;
            this.speed = speed;
            this.length = length;
        }

        //全列車情報の表示・取得
        public void ShowInfo()
        {

        }

        //情報の設定（・取得）
        public string ClientId
        {
            set { clientId = value; }
            get { return clientId; }
        }
        public string TrainNumber
        {
            set { trainNumber = value; }
            get { return trainNumber; }
        }
        public double Location
        {
            set { location = value; }
            get { return location; }
        }
        public double Speed
        {
            set { speed = value; }
            get { return speed; }
        }
        public int Length
        {
            set { length = value; }
            get { return length; }
        }
    }
    //旧コード
    public class LocationSpeedData
    {
        public double Location { get; set; }
        public double Speed { get; set; }
        public int Length { get; set; }
    }
    public class TrainStatus
    {
        public string TrainNumber { get; set; }
        public List<LocationSpeedData> Data { get; set; }
    }
    public class ClientData
    {
        public string ClientId { get; set; }
        public List<TrainStatus> Status { get; set; }
    }
}
