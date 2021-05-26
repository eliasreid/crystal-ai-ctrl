using System;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.WebSockets;

namespace CrystalAiCtrl
{

    //Put networking logic in here.

    //Interface.
    class WsClient
    {
        public enum ConnectResult
        {
            Success,
            Failure
        }

        ClientWebSocket? ws;

        Action<ArraySegment<byte>>? rxMessageCb = null;
        byte[] rxBuffer = new byte[4096];
        int rxBytes = 0;

        Thread? rxThread;

        //Asynchronously connect, and return either error or session ID
        public async Task<ConnectResult> Connect(Uri uri)
        {
            Disconnect();

            ws = new ClientWebSocket();
            Console.WriteLine("before ConnectAsync");
            var cancel = new CancellationTokenSource();
            Task connectTask = ws.ConnectAsync(uri, cancel.Token);
            if(await Task.WhenAny(connectTask, Task.Delay(2000)) == connectTask 
                && ws.State == WebSocketState.Open)
            {
                Console.WriteLine("connection made, starting rx thread");
                rxThread = new Thread(new ThreadStart(ReceiveThread));
                rxThread.Start();
                return ConnectResult.Success;
            }
            else
            {
                //connection timed out
                Console.WriteLine($"Failed to connect to server at {uri}");
                cancel.Cancel();
                return ConnectResult.Failure;
            }
        }

        public bool Connected()
        {
            return ws != null && ws.State == WebSocketState.Open;
        }

        public void MessageReceiveCallback(Action<ArraySegment<byte>> msgHandler)
        {
            rxMessageCb = msgHandler;
        }

        public void SendMessage(string sendData)
        {
            if(ws != null && ws.State == WebSocketState.Open)
            {
                //TODO: not sure if it's ok to block here, probably?
                var bytes = Encoding.UTF8.GetBytes(sendData);
                ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
            }
        }

        private void ReceiveThread()
        {
            while (ws != null && ws.State == WebSocketState.Open)
            {
                //add received data to end of buffer
                Console.WriteLine("waiting for message..");
                var rxTask = ws.ReceiveAsync(new ArraySegment<byte>(rxBuffer, rxBytes, rxBuffer.Length - rxBytes), CancellationToken.None);
                //Just swallow exception here
                try
                {
                    rxTask.Wait();
                }
                catch 
                {
                    //Just quit thread if exception is thrown.. Probably something more graceful we should do
                    return;
                }

                if (!rxTask.IsCompleted)
                {
                    //Receive task was cancelled, only happens when we're joining thread, just return.
                    return;
                }

                var rxResult = rxTask.Result;
                rxBytes += rxResult.Count;
                Console.WriteLine($"message received, size {rxResult.Count}, eom? {rxResult.EndOfMessage}");

                //Check if eom
                if (rxMessageCb != null && rxResult.EndOfMessage == true)
                {
                    Console.WriteLine($"calling callback");
                    rxMessageCb(new ArraySegment<byte>(rxBuffer, 0, rxBytes));
                    rxBytes = 0;
                }

                //TODO: handle close message received properly??
            }
        }

        public void Disconnect()
        {
            //Disposing the websocket will cancel the blocking ReceiveAsync in the ReceiveThread
            ws?.Dispose();
            if (rxThread != null && rxThread.ThreadState != ThreadState.Unstarted)
            {
                rxThread.Join();
            }
        }
    }
}
