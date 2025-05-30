﻿/*
 * This file is subject to the terms and conditions defined in
 * file 'license.txt', which is part of this source code package.
 */

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Org.Mentalis.Network.ProxySocket;

namespace SteamKit2
{
    class TcpConnection : IConnection
    {
        const uint MAGIC = 0x31305456; // "VT01"

        private ILogContext log;
        private Socket? socket;
        private Thread? netThread;
        private NetworkStream? netStream;
        private BinaryReader? netReader;
        private BinaryWriter? netWriter;

        private CancellationTokenSource? cancellationToken;
        private object netLock;

        public TcpConnection( ILogContext log )
        {
            this.log = log ?? throw new ArgumentNullException( nameof(log) );
            netLock = new object();
        }

        public event EventHandler<NetMsgEventArgs>? NetMsgReceived;

        public event EventHandler? Connected;

        public event EventHandler<DisconnectedEventArgs>? Disconnected;

        public EndPoint? CurrentEndPoint { get; private set; }
        public Proxy? Proxy { get; set; }
        public ProtocolTypes ProtocolTypes => ProtocolTypes.Tcp;

        private void Shutdown()
        {
            try
            {
                if ( socket != null && socket.Connected )
                {
                    socket.Shutdown( SocketShutdown.Both );
                    socket.Disconnect( true );
                }
            }
            catch
            {
                // Shutdown is throwing when the remote end closes the connection before SteamKit attempts to
                // so this should be safe as a no-op
                // see: https://bitbucket.org/VoiDeD/steamre/issue/41/socketexception-thrown-when-closing
            }
        }

        private void Release( bool userRequestedDisconnect )
        {
            lock ( netLock )
            {
                if ( cancellationToken != null )
                {
                    cancellationToken.Dispose();
                    cancellationToken = null;
                }

                if ( netWriter != null )
                {
                    netWriter.Dispose();
                    netWriter = null;
                }

                if ( netReader != null )
                {
                    netReader.Dispose();
                    netReader = null;
                }

                if ( netStream != null )
                {
                    netStream.Dispose();
                    netStream = null;
                }

                if ( socket != null )
                {
                    socket.Dispose();
                    socket = null;
                }
            }

            Disconnected?.Invoke( this, new DisconnectedEventArgs( userRequestedDisconnect ) );
        }

        private void ConnectCompleted( bool success )
        {
            // Always discard result if our request was cancelled
            // If we have no cancellation token source, we were already Release()'ed
            if ( cancellationToken?.IsCancellationRequested ?? true )
            {
                log.LogDebug( nameof(TcpConnection), "Connection request to {0} was cancelled", CurrentEndPoint );
                if ( success ) Shutdown();
                Release( userRequestedDisconnect: true );
                return;
            }
            else if ( !success )
            {
                log.LogDebug( nameof(TcpConnection), "Failed connecting to {0}", CurrentEndPoint );
                Release( userRequestedDisconnect: false );
                return;
            }

            log.LogDebug( nameof(TcpConnection), "Connected to {0}", CurrentEndPoint );
            DebugLog.Assert( socket != null, nameof(TcpConnection), "Socket should be non-null after connecting." );

            try
            {
                lock ( netLock )
                {
                    netStream = new NetworkStream( socket, false );
                    netReader = new BinaryReader( netStream );
                    netWriter = new BinaryWriter( netStream );

                    netThread = new Thread( NetLoop )
                    {
                        Name = "SK2-TcpConn"
                    };

                    CurrentEndPoint = socket!.RemoteEndPoint;
                }

                netThread.Start();

                Connected?.Invoke( this, EventArgs.Empty );
            }
            catch ( Exception ex )
            {
                log.LogDebug( nameof(TcpConnection), "Exception while setting up connection to {0}: {1}",
                    CurrentEndPoint, ex );
                Release( userRequestedDisconnect: false );
            }
        }

        private void TryConnect( int timeout )
        {
            DebugLog.Assert( cancellationToken != null, nameof(TcpConnection), "null CancellationToken in TryConnect" );

            if ( cancellationToken.IsCancellationRequested )
            {
                log.LogDebug( nameof(TcpConnection), "Connection to {0} cancelled by user", CurrentEndPoint );
                Release( userRequestedDisconnect: true );
                return;
            }

            DebugLog.Assert( socket != null, nameof(TcpConnection),
                "socket should not be null when connecting (we hold the net lock)" );
            DebugLog.Assert( CurrentEndPoint != null, nameof(TcpConnection),
                "CurrentEndPoint should be non-null when connecting." );

            try
            {
                using var timeoutTokenSource = new CancellationTokenSource( timeout );
                using var connectCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource( cancellationToken.Token,
                        timeoutTokenSource.Token );

                using ( connectCancellation.Token.Register( s => ( ( Socket )s! ).Dispose(), socket ) )
                {
                    if ( socket is ProxySocket proxySocket ) proxySocket.Connect( ToIpEndPoint( CurrentEndPoint ) ); // TODO: Update the proxy socket lib to correctly handle DNS endpoints
                    else socket.Connect( CurrentEndPoint );
                }
            }
            catch ( SocketException ) when ( cancellationToken.IsCancellationRequested )
            {
                // Ignore, this will be handled by ConnectCompleted.
            }
            catch ( Exception ex )
            {
                log.LogDebug( nameof(TcpConnection), "Exception while connecting to {0}: {1}", CurrentEndPoint, ex );
            }

            ConnectCompleted( socket.Connected );
        }

        /// <summary>
        /// Connects to the specified end point.
        /// </summary>
        /// <param name="endPoint">The end point to connect to.</param>
        /// <param name="timeout">Timeout in milliseconds</param>
        public void Connect( EndPoint endPoint, int timeout )
        {
            lock ( netLock )
            {
                DebugLog.Assert( cancellationToken == null, nameof(TcpConnection),
                    "Connection cancellation token is not null" );
                cancellationToken = new CancellationTokenSource();

                socket = CreateSocket();
                socket.ReceiveTimeout = timeout;
                socket.SendTimeout = timeout;

                CurrentEndPoint = endPoint;
                log.LogDebug( nameof(TcpConnection), "Connecting to {0}...", CurrentEndPoint );
                TryConnect( timeout );
            }
        }

        public void Disconnect( bool userInitiated )
        {
            lock ( netLock )
            {
                cancellationToken?.Cancel();

                Disconnected?.Invoke( this, new DisconnectedEventArgs( userInitiated ) );
            }
        }

        // this is now a steamkit meme
        /// <summary>
        /// Nets the loop.
        /// </summary>
        void NetLoop()
        {
            // poll for readable data every 100ms
            const int POLL_MS = 100;

            DebugLog.Assert( cancellationToken != null, nameof(TcpConnection), "null cancellationToken in NetLoop" );

            while ( !cancellationToken.IsCancellationRequested )
            {
                bool canRead = false;

                try
                {
                    canRead = socket!.Poll( POLL_MS * 1000, SelectMode.SelectRead );
                }
                catch ( SocketException ex )
                {
                    log.LogDebug( nameof(TcpConnection), "Socket exception while polling: {0}", ex );
                    break;
                }

                if ( !canRead )
                {
                    // nothing to read yet
                    continue;
                }

                byte[] packData;

                try
                {
                    // read the packet off the network
                    packData = ReadPacket();
                }
                catch ( IOException ex )
                {
                    log.LogDebug( nameof(TcpConnection), "Socket exception occurred while reading packet: {0}", ex );
                    break;
                }

                try
                {
                    NetMsgReceived?.Invoke( this, new NetMsgEventArgs( packData, CurrentEndPoint! ) );
                }
                catch ( Exception ex )
                {
                    log.LogDebug( nameof(TcpConnection), "Unexpected exception propogated back to NetLoop: {0}", ex );
                }
            }

            // Thread is shutting down, ensure socket is shut down and disposed
            bool userShutdown = cancellationToken.IsCancellationRequested;

            if ( userShutdown )
            {
                Shutdown();
            }

            Release( userShutdown );
        }

        byte[] ReadPacket()
        {
            // the tcp packet header is considerably less complex than the udp one
            // it only consists of the packet length, followed by the "VT01" magic
            uint packetLen;
            uint packetMagic;

            try
            {
                packetLen = netReader!.ReadUInt32();
                packetMagic = netReader.ReadUInt32();
            }
            catch ( IOException ex )
            {
                throw new IOException( "Connection lost while reading packet header.", ex );
            }

            if ( packetMagic != TcpConnection.MAGIC )
            {
                throw new IOException( "Got a packet with invalid magic!" );
            }

            // rest of the packet is the physical data
            byte[] packData = netReader.ReadBytes( ( int )packetLen );

            if ( packData.Length != packetLen )
            {
                throw new IOException( "Connection lost while reading packet payload" );
            }

            return packData;
        }

        public void Send( Memory<byte> data )
        {
            lock ( netLock )
            {
                if ( socket == null || netStream == null )
                {
                    log.LogDebug( nameof(TcpConnection), "Attempting to send client data when not connected." );
                    return;
                }

                try
                {
                    netWriter!.Write( ( uint )data.Length );
                    netWriter.Write( MAGIC );
                    netWriter.Write( data.Span );
                }
                catch ( IOException ex )
                {
                    log.LogDebug( nameof(TcpConnection), "Socket exception while writing data: {0}", ex );
                }
            }
        }

        public IPAddress GetLocalIP()
        {
            lock ( netLock )
            {
                if ( socket == null )
                {
                    return IPAddress.None;
                }

                try
                {
                    return NetHelpers.GetLocalIP( socket );
                }
                catch ( Exception ex )
                {
                    log.LogDebug( nameof(TcpConnection), "Socket exception trying to read bound IP: {0}", ex );
                    return IPAddress.None;
                }
            }
        }

        private Socket CreateSocket()
        {
            if ( Proxy is null ) return new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );
            
            var sock = new ProxySocket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp );

            sock.ProxyType = Proxy.Scheme switch
            {
                ProxyScheme.Http => ProxyTypes.Https,
                ProxyScheme.Socks4 => ProxyTypes.Socks4,
                ProxyScheme.Socks5 => ProxyTypes.Socks5,
                _ => throw new ArgumentOutOfRangeException( nameof( Proxy.Scheme ), Proxy.Scheme, null )
            };

            sock.ProxyEndPoint = ToIpEndPoint( new DnsEndPoint( Proxy.Host, Proxy.Port ) );

            if ( !string.IsNullOrWhiteSpace( Proxy.Username ) && !string.IsNullOrWhiteSpace( Proxy.Password ) )
            {
                sock.ProxyUser = Proxy.Username;
                sock.ProxyPass = Proxy.Password;
            }

            return sock;
        }

        private static IPEndPoint ToIpEndPoint( EndPoint endpoint )
        {
            switch (endpoint)
            {
                case IPEndPoint ipEndpoint:
                    return ipEndpoint;
                case DnsEndPoint dnsEndpoint:
                {
                    var ipList = Dns.GetHostAddresses( dnsEndpoint.Host, AddressFamily.InterNetwork );

                    if ( ipList.Length == 0 )
                    {
                        throw new ArgumentException( "Failed to resolve given DNS endpoint for the proxy.",
                            nameof(endpoint) );
                    }

                    return new IPEndPoint( ipList[ 0 ], dnsEndpoint.Port );
                }
                default:
                    throw new ArgumentException( "endPoint must be either a DnsEndPoint or an IPEndPoint.", nameof(endpoint) );
            }
        }
    }
}
