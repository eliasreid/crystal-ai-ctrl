using System;
using System.Collections.Generic;
using System.Linq;
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

        ClientWebSocket ws = new ClientWebSocket();

        Action<ArraySegment<byte>>? rxMessageCb = null;
        byte[] rxBuffer = new byte[4096];
        int rxBytes = 0;

        Thread? rxThread;

        //Asynchronously connect, and return either error or session ID
        public async Task<ConnectResult> Connect(Uri uri)
        {
            Console.WriteLine("before ConnectAsync");
            Task connectTask = ws.ConnectAsync(uri, CancellationToken.None);
            if(await Task.WhenAny(connectTask, Task.Delay(5000)) == connectTask 
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
                ws.Dispose();
                return ConnectResult.Failure;
            }
        }

        public bool Connected()
        {
            return ws.State == WebSocketState.Open;
        }

        public void MessageReceiveCallback(Action<ArraySegment<byte>> msgHandler)
        {
            rxMessageCb = msgHandler;
        }

        public void SendMessage(ArraySegment<byte> sendData)
        {
            if(ws.State == WebSocketState.Open)
            {
                //TODO: not sure if it's ok to block here, probably?
                ws.SendAsync(sendData, WebSocketMessageType.Text, true, CancellationToken.None).Wait();
            }
        }

        private void ReceiveThread()
        {
            Console.WriteLine($"thread first line, connected? {ws.State == WebSocketState.Open}");
            while (ws.State == WebSocketState.Open)
            {
                //add received data to end of buffer
                Console.WriteLine("waiting for message..");
                var rxTask = ws.ReceiveAsync(new ArraySegment<byte>(rxBuffer, rxBytes, rxBuffer.Length - rxBytes), CancellationToken.None);
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

            }
        }


    }
}
