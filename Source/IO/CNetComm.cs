using Celeste.Mod.CelesteNet;
using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.CelesteNet.Client.Components;
using Celeste.Mod.CelesteNet.DataTypes;
using Celeste.Mod.Deathlink.Data;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mod.Deathlink.IO
{
  public class CNetComm : GameComponent
  {
    public static CNetComm Instance { get; private set; }

    #region Events

    public delegate void OnConnectedHandler(CelesteNetClientContext cxt);
    public static event OnConnectedHandler OnConnected;

    public delegate void OnReceiveConnectionInfoHandler(DataConnectionInfo data);
    public static event OnReceiveConnectionInfoHandler OnReceiveConnectionInfo;

    public delegate void OnDisonnectedHandler(CelesteNetConnection con);
    public static event OnDisonnectedHandler OnDisconnected;

    public delegate void OnReceiveDeathlinkUpdateHandler(DeathlinkUpdate data);
    public static event OnReceiveDeathlinkUpdateHandler OnReceiveDeathlinkUpdate;

    #endregion

    #region Local State Information

    public CelesteNetClientContext CnetContext { get { return CelesteNetClientModule.Instance?.Context; } }

    public CelesteNetClient CnetClient { get { return CelesteNetClientModule.Instance?.Client; } }
    public bool IsConnected { get { return CnetClient?.Con?.IsConnected ?? false; } }
    public uint? CnetID { get { return IsConnected ? (uint?)CnetClient?.PlayerInfo?.ID : null; } }
    public long MaxPacketSize { get { return CnetClient?.Con is CelesteNetTCPUDPConnection connection ? (connection.ConnectionSettings?.MaxPacketSize ?? 2048) : 2048; } }

    public DataChannelList.Channel CurrentChannel
    {
      get
      {
        if (!IsConnected) return null;
        KeyValuePair<Type, CelesteNetGameComponent> listComp = CnetContext.Components.FirstOrDefault((KeyValuePair<Type, CelesteNetGameComponent> kvp) =>
        {
          return kvp.Key == typeof(CelesteNetPlayerListComponent);
        });
        if (listComp.Equals(default(KeyValuePair<Type, CelesteNetGameComponent>))) return null;
        CelesteNetPlayerListComponent comp = listComp.Value as CelesteNetPlayerListComponent;
        DataChannelList.Channel[] list = comp.Channels?.List;
        return list?.FirstOrDefault(c => c.Players.Contains(CnetClient.PlayerInfo.ID));
      }
    }
    public bool CurrentChannelIsMain
    {
      get
      {
        return CurrentChannel?.Name?.ToLower() == "main";
      }
    }

    public bool CanSendMessages
    {
      get
      {
        return IsConnected /*&& !CurrentChannelIsMain*/;
      }
    }

    private ConcurrentQueue<Action> updateQueue = new ConcurrentQueue<Action>();

    #endregion

    #region Setup

    public CNetComm(Game game)
      : base(game)
    {
      Instance = this;
      Disposed += OnComponentDisposed;
      CelesteNetClientContext.OnStart += OnCNetClientContextStart;
      CelesteNetClientContext.OnDispose += OnCNetClientContextDispose;
    }

    private void OnComponentDisposed(object sender, EventArgs args)
    {
      CelesteNetClientContext.OnStart -= OnCNetClientContextStart;
      CelesteNetClientContext.OnDispose -= OnCNetClientContextDispose;
    }

    #endregion

    #region Hooks + Events

    private void OnCNetClientContextStart(CelesteNetClientContext cxt)
    {
      CnetClient.Data.RegisterHandlersIn(this);
      CnetClient.Con.OnDisconnect += OnDisconnect;
      updateQueue.Enqueue(() => OnConnected?.Invoke(cxt));
    }

    private void OnCNetClientContextDispose(CelesteNetClientContext cxt)
    {
      // CnetClient is null here
    }

    private void OnDisconnect(CelesteNetConnection con)
    {
      updateQueue.Enqueue(() => OnDisconnected?.Invoke(con));
    }

    public override void Update(GameTime gameTime)
    {
      ConcurrentQueue<Action> queue = updateQueue;
      updateQueue = new ConcurrentQueue<Action>();
      foreach (Action act in queue)
      {
        act();
      }

      DeathlinkModule.Instance.Update(gameTime);

      base.Update(gameTime);
    }

    #endregion

    #region Entry Points

    internal void Send<T>(T data, bool sendToSelf) where T : DataType<T>
    {
      if (!CanSendMessages)
      {
        return;
      }
      try
      {
        if (sendToSelf) CnetClient.SendAndHandle(data);
        else CnetClient.Send(data);
      }
      catch (Exception e)
      {
        // "The only way I know of for this to happen is a well-timed connection blorp but just in case" -corkr900
        Logger.Log(LogLevel.Error, "Deathlink/CNetComm", $"Exception was handled in Deathlink.IO.CNetComm.Send<{typeof(T).Name}>");
        Logger.LogDetailed(LogLevel.Error, "Deathlink/CNetComm", e.Message);
      }
    }

    #endregion

    public bool isSameChannel(string channel)
    {
      return CurrentChannel?.Name == channel;
    }

    #region Message Handlers

    public void Handle(CelesteNetConnection con, DataConnectionInfo data)
    {
      if (data.Player == null) data.Player = CnetClient.PlayerInfo;  // It's null when handling our own messages
      updateQueue.Enqueue(() => OnReceiveConnectionInfo?.Invoke(data));
    }

    public void Handle(CelesteNetConnection con, DeathlinkUpdate data)
    {
      if (data.player == null) data.player = CnetClient.PlayerInfo;  // It's null when handling our own messages
      if (!isSameChannel(data.cnetChannel)) return;
      updateQueue.Enqueue(() => OnReceiveDeathlinkUpdate?.Invoke(data));
      Logger.Log(LogLevel.Debug, "Deathlink/CNetComm", $"Received DeathlinkUpdate: {data}");
    }

    #endregion
  }
}
