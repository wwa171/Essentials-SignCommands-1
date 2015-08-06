using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace Essentials {
  [ApiVersion(1, 20)]
  public class Essentials : TerrariaPlugin {
    public override string Name { get { return "Essentials"; } }
    public override string Author { get { return "Scavenger"; } }
    public override string Description { get { return "Some Essential commands for TShock!"; } }
    public override Version Version { get { return new Version(1, 6, 0); } }

    private readonly Dictionary<string, int[]> _disabled = new Dictionary<string, int[]>();
    private DateTime _lastCheck = DateTime.UtcNow;

    public Essentials(Main game)
      : base(game) {
      Order = 4;
    }

    public override void Initialize() {
      ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
    }

    protected override void Dispose(bool disposing) {
      if (disposing) {
        ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);

      }
      base.Dispose(disposing);
    }

    private void OnInitialize(EventArgs args) {
      Commands.ChatCommands.Add(new Command("essentials.more", CmdMore, "more"));
      Commands.ChatCommands.Add(new Command("essentials.helpop.ask", CmdHelpOp, "helpop", "staffhelp"));
      Commands.ChatCommands.Add(new Command("essentials.suicide", CmdSuicide, "suicide", "die"));
    }

    /* Commands: */

    #region More
    private static void CmdMore(CommandArgs args) {
      if (args.Parameters.Count > 0 && args.Parameters[0].ToLower() == "all") {
        var full = true;
        foreach (var item in args.TPlayer.inventory) {
          if (item == null || item.stack == 0) continue;
          var amtToAdd = item.maxStack - item.stack;
          if (item.stack > 0 && amtToAdd > 0 && !item.name.ToLower().Contains("coin")) {
            full = false;
            args.Player.GiveItem(item.type, item.name, item.width, item.height, amtToAdd);
          }
        }
        if (!full)
          args.Player.SendSuccessMessage("Filled all your items.");
        else
          args.Player.SendErrorMessage("Your inventory is already full.");
      }
      else {
        var holding = args.Player.TPlayer.inventory[args.TPlayer.selectedItem];
        var amtToAdd = holding.maxStack - holding.stack;
        if (holding.stack > 0 && amtToAdd > 0)
          args.Player.GiveItem(holding.type, holding.name, holding.width, holding.height, amtToAdd);
        if (amtToAdd == 0)
          args.Player.SendErrorMessage("Your {0} is already full.", holding.name);
        else
          args.Player.SendSuccessMessage("Filled up your {0}.", holding.name);
      }
    }
    #endregion

    #region HelpOp
    private void CmdHelpOp(CommandArgs args) {
      if (args.Parameters.Count < 1) {
        args.Player.SendErrorMessage("Usage: /helpop <message>");
        return;
      }

      var text = string.Join(" ", args.Parameters);

      int staffNum = 0;

      foreach (var TPlr in TShock.Players) {
        if (TPlr != null && TPlr.Group.HasPermission("essentials.helpop.receive")) {
          staffNum++;
          TPlr.SendMessage(string.Format("[HelpOp] {0}: {1}", args.Player.Name, text),
                    Color.Magenta);
        }
      }

      if (staffNum == 0) {
        args.Player.SendMessage("[HelpOp] There are no operators online to receive your message.", Color.Magenta);
      }
      else {
        args.Player.SendMessage(string.Format("[HelpOp] Your message has been received by {0} operator{1}",
                    staffNum, staffNum == 1 ? "" : "s"), Color.Magenta);
      }
    }
    #endregion

    #region Suicide
    private static void CmdSuicide(CommandArgs args) {
      if (!args.Player.RealPlayer)
        return;
      NetMessage.SendData(26, -1, -1, " decided it wasnt worth living.", args.Player.Index, 0, 15000);
    }
    #endregion
  }
}
