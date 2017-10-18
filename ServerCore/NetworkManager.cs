using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace TestServer.ServerCore
{
    class NetworkManager
    {
        private int maxClient;
        private int clientCount;
        private TcpListener tcplistener;

        private List<Object> clientPool;


        public bool IsRunning { get; private set; }

        public IPAddress IP { get; private set; }

        public int Port { get; private set; }

        public Encoding Encoding { get; set; }

        public NetworkManager(IPAddress ip, int port)
        {
            IP = ip;
            Port = port; ;
            Encoding = Encoding.Default;
            clientPool = new List<object>();
            tcplistener = new TcpListener(IP, Port);
        }

        public void Start(int maxCount = 0)
        {
            if (IsRunning == false)
            {
                IsRunning = true;
                if (maxCount == 0)
                    tcplistener.Start();
                else
                    tcplistener.Start(maxCount);

                tcplistener.BeginAcceptTcpClient(new AsyncCallback(HandleTcpClientAccepted), tcplistener);
                Console.WriteLine("服务器启动");
            }
        }



        public void Stop()
        {
            if (IsRunning)
            {
                IsRunning = false;
                tcplistener.Stop();
                lock (clientPool)
                {
                    CloseAllClient();
                }
            }
        }

        public void Close(TcpClientState state)
        {
            if (state != null)
            {
                state.Close();
                clientPool.Remove(state);
                clientCount--;
            }
        }


        public void CloseAllClient()
        {
            foreach (TcpClientState client in clientPool)
            {
                Close(client);
            }
            clientCount = 0;
            clientPool.Clear();
        }


        #region Handle
        public void HandleTcpClientAccepted(IAsyncResult ar)
        {
            if (IsRunning)
            {
                TcpClient client = tcplistener.EndAcceptTcpClient(ar);
                byte[] buffer = new byte[client.ReceiveBufferSize];
                TcpClientState state = new TcpClientState(client, buffer);
                lock (clientPool)
                {
                    clientPool.Add(client);
                    RaiseClientConnected(state);
                }
                NetworkStream stream = state.NetworkStream;
                stream.BeginRead(state.Buffer, 0, state.Buffer.Length, HandleDataReceive, state);
                tcplistener.BeginAcceptTcpClient(new AsyncCallback(HandleTcpClientAccepted), ar.AsyncState);
            }
        }

        public void HandleDataReceive(IAsyncResult ar)
        {
            if (IsRunning)
            {
                TcpClientState state = (TcpClientState)ar.AsyncState;
                NetworkStream stream = state.NetworkStream;
                int recv = 0;
                try
                {
                    recv = stream.EndRead(ar);
                }
                catch
                {
                    recv = 0;
                }

                if (recv == 0)
                {
                    lock (clientPool)
                    {
                        clientPool.Remove(state);
                        return;
                    }
                }

                byte[] buff = new byte[recv];
                Buffer.BlockCopy(state.Buffer, 0, buff, 0, recv);
                RaiseDataReceived(state);
                stream.BeginRead(state.Buffer, 0, state.Buffer.Length, HandleDataReceive, state);

            }
        }


        public void Send(TcpClientState state, byte[] data)
        {
            RaisePrepareSend(state);
            Send(state.TcpClient, data);
        }

        public void Send(TcpClient client, byte[] data)
        {
            if (IsRunning == false)
                throw new InvalidProgramException("This TCP Scoket server has not been started.");

            if (client == null)
                throw new ArgumentNullException("client");

            if (data == null)
                throw new ArgumentNullException("data");

            client.GetStream().BeginWrite(data, 0, data.Length, SendDataEnd, client);
        }

        public void SendDataEnd(IAsyncResult ar)
        {
            ((TcpClient)ar.AsyncState).GetStream().EndWrite(ar);
            RaiseCompletedSend(null);
        }
        #endregion

        #region 事件
        public event EventHandler<AsyncEventArgs> clientConnected;
        public event EventHandler<AsyncEventArgs> ClientDisconnected;
        public event EventHandler<AsyncEventArgs> DataReceived;
        public event EventHandler<AsyncEventArgs> PrepareSend;
        public event EventHandler<AsyncEventArgs> CompletedSend;
        public event EventHandler<AsyncEventArgs> NetError;
        public event EventHandler<AsyncEventArgs> OtherException;

        private void RaiseClientConnected(TcpClientState state)
        {
            Console.WriteLine("当前客户端连接数:{0}", clientCount.ToString());
            if (ClientDisconnected != null)
            {
                clientConnected(this, new AsyncEventArgs(state));
            }
        }

        private void RaiseClientDisconnected(TcpClientState state)
        {
            if (ClientDisconnected != null)
            {
                ClientDisconnected(this, new AsyncEventArgs("连接断开"));
            }
        }


        private void RaiseDataReceived(TcpClientState state)
        {

            ByteBuffer buff = new ByteBuffer(state.Buffer);

            Console.WriteLine(buff.ReadInt());
            Console.WriteLine(buff.ReadShort());
            Console.WriteLine(buff.ReadShort());
            Console.WriteLine(buff.ReadInt());
            Console.WriteLine(buff.ReadString());

            if (DataReceived != null)
            {
                DataReceived(this, new AsyncEventArgs(state));
            }
        }

        private void RaisePrepareSend(TcpClientState state)
        {
            if (PrepareSend != null)
            {
                PrepareSend(this, new AsyncEventArgs(state));
            }
        }

        private void RaiseCompletedSend(TcpClientState state)
        {
            if (CompletedSend != null)
            {
                CompletedSend(this, new AsyncEventArgs(state));
            }
        }

        private void RaiseNetError(TcpClientState state)
        {
            if (NetError != null)
            {
                NetError(this, new AsyncEventArgs(state));
            }
        }

        private void RaiseOtherException(TcpClientState state, string descrip)
        {
            if (OtherException != null)
            {
                OtherException(this, new AsyncEventArgs(descrip, state));
            }
        }

        private void RaiseOtherException(TcpClientState state)
        {
            RaiseOtherException(state, "");
        }
        #endregion
    }

    public class TcpClientState
    {
        public TcpClient TcpClient { get; private set; }
        public byte[] Buffer { get; private set; }

        public NetworkStream NetworkStream
        {
            get
            {
                return TcpClient.GetStream();
            }
        }

        public TcpClientState(TcpClient client, byte[] buffer)
        {
            if (client == null)
            {
                throw new ArgumentNullException("tcpclient");
            }

            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            this.TcpClient = client;
            this.Buffer = buffer;
        }

        public void Close()
        {
            this.TcpClient.Close();
            Buffer = null;
        }
    }

    public class AsyncEventArgs : EventArgs
    {
        public string msg;
        public TcpClientState state;
        public bool IsHandled { get; set; }
        public AsyncEventArgs(TcpClientState state)
        {
            this.state = state;
            IsHandled = true;
        }

        public AsyncEventArgs(string msg, TcpClientState state)
        {
            this.msg = msg;
            this.state = state;
            IsHandled = false;
        }

        public AsyncEventArgs(string msg)
        {
            this.msg = msg;
            IsHandled = false;
        }

    }
}
