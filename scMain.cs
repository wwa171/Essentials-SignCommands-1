using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Xna.Framework;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Timer = System.Timers.Timer;

namespace SignCommands {
  //TODO: Add infinite signs support & Global cooldowns
  [ApiVersion(2, 1)]
  public class SignCommands : TerrariaPlugin {
    public override string Name => "Sign Commands";
    public override string Author => "Scavenger";
    public override string Description => "Put commands on signs!";
    public override Version Version => new Version(1, 6, 0);

    public static ScConfig config = new ScConfig();
    public static readonly ScPlayer[] ScPlayers = new ScPlayer[256];

    public static readonly List<Cooldown> Cooldowns = new List<Cooldown>();

    private static readonly Dictionary<Point, ScSign> ScSigns = new Dictionary<Point, ScSign>();

    private readonly Timer _updateTimer = new Timer { Enabled = true, Interval = 1000d };

    public SignCommands(Main game)
      : base(game) {
    }

    public override void Initialize() {
      ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
      ServerApi.Hooks.NetGetData.Register(this, OnGetData);
      ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
      ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
    }

    protected override void Dispose(bool disposing) {
      if (disposing) {
        ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
        ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
        ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
        ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
      }
      base.Dispose(disposing);
    }

    private void OnInitialize(EventArgs args) {
      Commands.ChatCommands.Add(new Command("essentials.signs.break", CmdDestroySign, "destroysign", "dsign"));
      Commands.ChatCommands.Add(new Command("essentials.signs.reload", CmdReloadSigns, "screload"));

      var savePath = Path.Combine(TShock.SavePath, "Essentials");
      if (!Directory.Exists(savePath))
        Directory.CreateDirectory(savePath);
      var configPath = Path.Combine(savePath, "scConfig.json");
      (config = ScConfig.Read(configPath)).Write(configPath);

      _updateTimer.Elapsed += UpdateTimerOnElapsed;
    }


    #region Commands

    private void CmdDestroySign(CommandArgs args) {
      var sPly = ScPlayers[args.Player.Index];

      sPly.DestroyMode = true;
      args.Player.SendSuccessMessage("You can now destroy a sign.");
    }

    private void CmdReloadSigns(CommandArgs args) {
      var savePath = Path.Combine(TShock.SavePath, "Essentials");
      if (!Directory.Exists(savePath))
        Directory.CreateDirectory(savePath);
      var configPath = Path.Combine(savePath, "scConfig.json");
      (config = ScConfig.Read(configPath)).Write(configPath);

      args.Player.SendSuccessMessage("Sign Commands config has been reloaded.");
    }

    #endregion

    #region scPlayers

    private static void OnJoin(JoinEventArgs args) {
      ScPlayers[args.Who] = new ScPlayer(args.Who);

      //if (OfflineCooldowns.ContainsKey(TShock.Players[args.Who].Name))
      //{
      //ScPlayers[args.Who].Cooldowns = OfflineCooldowns[TShock.Players[args.Who].Name];
      //OfflineCooldowns.Remove(TShock.Players[args.Who].Name);
      //}
    }

    private static void OnLeave(LeaveEventArgs args) {
      //if (ScPlayers[args.Who] != null && ScPlayers[args.Who].Cooldowns.Count > 0)
      //    OfflineCooldowns.Add(TShock.Players[args.Who].Name, ScPlayers[args.Who].Cooldowns);
      ScPlayers[args.Who] = null;
    }

    #endregion

    #region Timer

    private void UpdateTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs) {
      foreach (var sPly in ScPlayers.Where(sPly => sPly != null)) {
        if (sPly.AlertCooldownCooldown > 0)
          sPly.AlertCooldownCooldown--;
        if (sPly.AlertPermissionCooldown > 0)
          sPly.AlertPermissionCooldown--;
        if (sPly.AlertDestroyCooldown > 0)
          sPly.AlertDestroyCooldown--;
      }

      foreach (var signPair in ScSigns) {
        var sign = signPair.Value;
        if (sign.cooldown > 0)
          sign.cooldown--;
      }
    }

    #endregion

    #region OnSignNew

    private static bool OnSignNew(int x, int y, string text, int who, int signIndex) {
      if (!text.StartsWith(config.DefineSignCommands, StringComparison.CurrentCultureIgnoreCase))
        return false;

      var tPly = TShock.Players[who];
      var sPly = ScPlayers[who];
      var point = new Point(x, y);

      if (tPly == null || sPly == null)
        return false;

      if (!ScUtils.CanCreate(tPly)) {
        SendErrorToPlayer(sPly, tPly, "You do not have the permission to create a Sign Commands sign.");
        return true;
      }

      ScSign sign;
      try {
        sign = new ScSign(text, tPly, point);
      } catch (Exception ex) {
        SendErrorToPlayer(sPly, tPly, ex.Message);
        return true;
      }

      if (!ScUtils.CanEdit(tPly, sign)) {
        SendErrorToPlayer(sPly, tPly, "This sign is protected from modifications.");
        return true;
      }

      Task.Factory.StartNew(() => {
        Thread.Sleep(10);

        // actually register the new command sign only, if the player had the permission to change the sign text.
        // This ensures that tshock (by regions) and other plugins protecting this sign have a chance to prevent the change.
        string newText = Main.sign[signIndex].text;
        bool textWasApplied = (newText == text);
        if (textWasApplied)
          ScSigns.AddItem(point, sign);
      });
      return false;
    }

    #endregion

    #region OnSignHit

    public static bool OnSignHit(int x, int y, string text, int who) {
      if (!text.ToLower().StartsWith(config.DefineSignCommands.ToLower())) return false;
      var tPly = TShock.Players[who];
      var sPly = ScPlayers[who];
      var sign = ScSigns.Check(x, y, text, tPly);

      if (tPly == null || sPly == null) return false;

      var canBreak = ScUtils.CanBreak(tPly);
      if (sPly.DestroyMode && canBreak) return false;

      if (config.ShowDestroyMessage && canBreak && sPly.AlertDestroyCooldown == 0) {
        tPly.SendInfoMessage("To destroy this sign, Type \"/destroysign\".");
        sPly.AlertDestroyCooldown = 10;
      }

      try {
        sign.ExecuteCommands(sPly);
      } catch (Exception ex) {
        SendErrorToPlayer(sPly, tPly, ex.Message);
      }

      return true;
    }

    #endregion

    #region OnSignKill

    private static bool OnSignKill(int x, int y, string text, int who) {
      if (!text.ToLower().StartsWith(config.DefineSignCommands.ToLower())) return false;

      var sPly = ScPlayers[who];
      var tPly = TShock.Players[who];
      if (sPly == null || tPly == null) return false;
      var sign = ScSigns.Check(x, y, text, sPly.TsPlayer);

      if (sPly.DestroyMode && ScUtils.CanBreak(sPly.TsPlayer)) {
        sPly.DestroyMode = false;
        //Cooldown removal
        return false;
      }

      try {
        sign.ExecuteCommands(sPly);
      } catch (Exception ex) {
        SendErrorToPlayer(sPly, tPly, ex.Message);
      }
      return true;
    }

    #endregion

    #region OnSignOpen

    private static bool OnSignOpen(int x, int y, string text, int who) {
      if (!text.ToLower().StartsWith(config.DefineSignCommands.ToLower())) return false;

      var tPly = TShock.Players[who];
      var sign = ScSigns.Check(x, y, text, tPly);

      if (!ScUtils.CanRead(tPly, sign))
        return true;

      return false;
    }

    #endregion

    #region OnGetData

    private static void OnGetData(GetDataEventArgs e) {
      if (e.Handled)
        return;

      using (var reader = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
        switch (e.MsgID) {
          #region Sign Edit

          case PacketTypes.SignNew: {
              reader.ReadInt16();
              var x = reader.ReadInt16();
              var y = reader.ReadInt16();
              var newText = reader.ReadString();
              var id = Sign.ReadSign(x, y);
              if (id < 0 || Main.sign[id] == null) return;
              x = (short)Main.sign[id].x;
              y = (short)Main.sign[id].y;
              if (OnSignNew(x, y, newText, e.Msg.whoAmI, id)) {
                e.Handled = true;
                TShock.Players[e.Msg.whoAmI].SendData(PacketTypes.SignNew, "", id);
              }
            }
            break;

          #endregion

          #region Sign Read

          case PacketTypes.SignRead: {
              var x = reader.ReadInt16();
              var y = reader.ReadInt16();
              var id = Sign.ReadSign(x, y);
              if (id < 0 || Main.sign[id] == null) return;
              x = (short)Main.sign[id].x;
              y = (short)Main.sign[id].y;
              var text = Main.sign[id].text;
              if (OnSignOpen(x, y, text, e.Msg.whoAmI)) {
                e.Handled = true;
                TShock.Players[e.Msg.whoAmI].SendErrorMessage("This sign is protected from viewing");
              }
            }
            break;

          #endregion

          #region Tile Modify

          case PacketTypes.Tile: {
              var action = reader.ReadByte();
              var x = reader.ReadInt16();
              var y = reader.ReadInt16();
              var type = reader.ReadUInt16();

              if (Main.tile[x, y].type != 55) return;

              var id = Sign.ReadSign(x, y);
              if (id < 0 || Main.sign[id] == null) return;
              x = (short)Main.sign[id].x;
              y = (short)Main.sign[id].y;
              var text = Main.sign[id].text;

              bool handle;
              if (action == 0 && type == 0)
                handle = OnSignKill(x, y, text, e.Msg.whoAmI);
              else if (action == 0)
                handle = OnSignHit(x, y, text, e.Msg.whoAmI);
              else
                handle = false;

              if (handle) {
                e.Handled = true;
                TShock.Players[e.Msg.whoAmI].SendTileSquare(x, y);
              }
            }
            break;

          #endregion
        }
    }

    #endregion

    private static void SendErrorToPlayer(ScPlayer sPly, TSPlayer tPly, string message) {
      if (sPly.AlertPermissionCooldown == 0) {
        tPly.SendErrorMessage(message);
        sPly.AlertPermissionCooldown = 3;
      }
    }
  }

  public class Cooldown {
    public int time;
    public ScSign sign;
    public string name;
    public string group;

    public Cooldown(int time, ScSign sign, string name, string group = null) {
      this.time = time;
      this.sign = sign;
      this.name = name;
      this.group = group;
    }
  }
}