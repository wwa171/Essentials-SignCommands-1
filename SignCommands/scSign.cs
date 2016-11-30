using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TShockAPI;

namespace SignCommands {
  public class ScSign {
    public int cooldown;
    private int _cooldown;
    private string _cooldownGroup;
    private readonly List<string> _groups = new List<string>();
    private readonly List<string> _users = new List<string>();
    public readonly Dictionary<List<string>, SignCommand> commands = new Dictionary<List<string>, SignCommand>();
    public bool freeAccess;
    public bool noEdit;
    public bool noRead;
    private bool confirm;
    private bool silent;

    public string requiredPermission = string.Empty;

    public ScSign(string text, TSPlayer registrar, Point point, bool checkPermissions = true) {
      cooldown = 0;
      _cooldownGroup = string.Empty;
      RegisterCommands(text, registrar, checkPermissions);
    }

    #region ParseCommands

    private IEnumerable<List<string>> ParseCommands(string text) {
      //Remove the Sign Command definer. It's not required
      text = text.Remove(0, SignCommands.config.DefineSignCommands.Length);

      //Replace the Sign Command command starter with the TShock one so that it gets handled properly later
      text = text.Replace(SignCommands.config.CommandsStartWith, TShock.Config.CommandSpecifier);

      //Remove whitespace
      text = text.Trim();

      //Create a local variable for our return value
      var ret = new List<List<string>>();

      //Split the text string at any TShock command character
      var cmdStrings = text.Split(Convert.ToChar(TShock.Config.CommandSpecifier));
      
      //Iterate through the strings
      foreach (var str in cmdStrings) {
        var sbList = new List<string>();
        var sb = new StringBuilder();
        var instr = false;
        for (var i = 0; i < str.Length; i++) {
          var c = str[i];

          if (c == '\\' && ++i < str.Length) {
            if (str[i] != '"' && str[i] != ' ' && str[i] != '\\')
              sb.Append('\\');
            sb.Append(str[i]);
          }
          else if (c == '"') {
            instr = !instr;
            if (!instr) {
              sbList.Add(sb.ToString());
              sb.Clear();
            }
            else if (sb.Length > 0) {
              sbList.Add(sb.ToString());
              sb.Clear();
            }
          }
          else if (IsWhiteSpace(c) && !instr) {
            if (sb.Length > 0) {
              sbList.Add(sb.ToString());
              sb.Clear();
            }
          }
          else
            sb.Append(c);
        }
        if (sb.Length > 0)
          sbList.Add(sb.ToString());

        ret.Add(sbList);
      }
      return ret;
    }

    private static bool IsWhiteSpace(char c) {
      return c == ' ' || c == '\t' || c == '\n';
    }

    #endregion

    #region RegisterCommands

    private void RegisterCommands(string text, TSPlayer ply, bool checkPermissions) {
      var cmdList = ParseCommands(text);

      foreach (var cmdArgs in cmdList) {
        var args = new List<string>(cmdArgs);
        if (args.Count < 1)
          continue;

        var cmdName = args[0];
        
        switch (cmdName) {
          case "no-perm":
            if (checkPermissions && !ply.Group.HasPermission("essentials.signs.negateperms"))
              throw new UnauthorizedAccessException("You do not have permission to use \"no-perm\" on a sign.");

            freeAccess = true;
            continue;
          case "confirm":
            confirm = true;
            continue;
          case "no-read":
            noRead = true;
            continue;
          case "no-edit":
            noEdit = true;
            continue;
          case "silent":
            silent = true;
            continue;
          case "require-perm":
          case "rperm":
            requiredPermission = args[1];
            continue;
          case "cd":
          case "cooldown":
            ParseSignCd(args);
            continue;
          case "allowg":
            if (checkPermissions && !ply.Group.HasPermission("essentials.signs.allowgroups"))
              throw new UnauthorizedAccessException("You do not have permission to use \"allowg\" on a sign.");

            ParseGroups(args);
            continue;
          case "allowu":
            if (checkPermissions && !ply.Group.HasPermission("essentials.signs.allowusers"))
              throw new UnauthorizedAccessException("You do not have permission to use \"allowu\" on a sign.");

            ParseUsers(args);
            continue;
        }

        IEnumerable<Command> cmds = Commands.ChatCommands.Where(c => c.HasAlias(cmdName));

        foreach (var cmd in cmds) {
          var sCmd = new SignCommand(cooldown, cmd.Permissions, cmd.CommandDelegate, cmdName);
          commands.Add(args, sCmd);
        }
      }

      if (checkPermissions)
        CheckCommandPermissions(ply);
    }

    #endregion

    #region ExecuteCommands

    public void ExecuteCommands(ScPlayer sPly) {
      CheckPermissions(sPly.TsPlayer);
      
      if (cooldown > 0) {
        if (!sPly.TsPlayer.Group.HasPermission("essentials.signs.nocd")) {
          if (sPly.AlertCooldownCooldown == 0) {
            sPly.TsPlayer.SendErrorMessage("This sign is still cooling down. Please wait {0} more second{1}",
              cooldown, cooldown.Suffix());
            sPly.AlertCooldownCooldown = 3;
          }

          return;
        }
      }

      if (confirm && sPly.confirmSign != this) {
        sPly.confirmSign = this;
        sPly.TsPlayer.SendWarningMessage("Are you sure you want to execute this sign command?");
        sPly.TsPlayer.SendWarningMessage("Hit the sign again to confirm.");
        cooldown = 2;
        return;
      }

      if (_groups.Count > 0 && !_groups.Contains(sPly.TsPlayer.Group.Name)) {
        if (sPly.AlertPermissionCooldown == 0) {
          sPly.TsPlayer.SendErrorMessage("Your group does not have access to this sign");
          sPly.AlertPermissionCooldown = 3;
        }
        return;
      }

      if (_users.Count > 0 && !_users.Contains(sPly.TsPlayer.User.Name)) {
        if (sPly.AlertPermissionCooldown == 0) {
          sPly.TsPlayer.SendErrorMessage("You do not have access to this sign");
          sPly.AlertPermissionCooldown = 3;
        }
        return;
      }
      
      foreach (var cmdPair in commands) {
        var cmd = cmdPair.Value;
        var cmdText = string.Join(" ", cmdPair.Key);
        cmdText = cmdText.Replace("{player}", sPly.TsPlayer.Name);
        //Create args straight from the command text, meaning no need to iterate through args to replace {player}
        var args = cmdText.Split(' ').ToList();


        string log = string.Format("{0} executed: {1}{2} [Via sign command].", 
          sPly.TsPlayer.Name, TShock.Config.CommandSpecifier, cmdText);

        if (!silent) 
          TShock.Utils.SendLogs(log, Color.PaleVioletRed, sPly.TsPlayer);
        else
          TShock.Log.Info(log);
          
        args.RemoveAt(0);

        cmd.CommandDelegate.Invoke(new CommandArgs(cmdText, silent, sPly.TsPlayer, args));
      }

      cooldown = _cooldown;
      sPly.AlertCooldownCooldown = 3;
      sPly.confirmSign = null;
    }

    #endregion

    #region CheckPermissions, CheckCommandPermissions

    public void CheckPermissions(TSPlayer player) {
      if (player == null)
        throw new ArgumentNullException("player");

      var sPly = SignCommands.ScPlayers[player.Index];
      if (sPly == null) {
        TShock.Log.ConsoleError("An error occured while executing a sign command. TSPlayer {0} at index {1} does not exist as an ScPlayer", player.Name, player.Index);
        throw new InvalidOperationException("An error occured. Please try again");
      }

      if (!string.IsNullOrEmpty(requiredPermission) && !player.Group.HasPermission(requiredPermission))
        throw new AccessViolationException("You do not have the required permission to use this sign.");

      if (!freeAccess)
        CheckCommandPermissions(player);
    }

    private void CheckCommandPermissions(TSPlayer player) {
      if (!this.commands.Values.All(command => command.CanRun(player))) {
        throw new AccessViolationException("You do not have access to at least one of the commands on this sign.");
      }
    }

    #endregion

    private void ParseSignCd(IList<string> args) {
      int cd;
      if (args.Count < 3) {
        //args[0] is command name
        if (!int.TryParse(args[1], out cd)) {
          if (SignCommands.config.CooldownGroups.ContainsKey(args[1])) {
            cd = SignCommands.config.CooldownGroups[args[1]];
            _cooldownGroup = args[1];
          }
        }
        _cooldown = cd;
      }
      else {
        //args[0] is command name. args[1] is cooldown specifier. args[2] is cooldown
        if (string.Equals(args[1], "global", StringComparison.CurrentCultureIgnoreCase)) {
          if (!int.TryParse(args[2], out cd)) {
            if (SignCommands.config.CooldownGroups.ContainsKey(args[2])) {
              cd = SignCommands.config.CooldownGroups[args[2]];
              _cooldownGroup = args[2];
            }
          }
          _cooldown = cd;
        }
      }
    }

    private void ParseGroups(IEnumerable<string> args) {
      var groups = new List<string>(args);
      //Remove the command name- it's not a group
      groups.RemoveAt(0);

      foreach (var group in groups)
        _groups.Add(group);
    }

    private void ParseUsers(IEnumerable<string> args) {
      var users = new List<string>(args);
      //Remove the command name- it's not a user
      users.RemoveAt(0);

      foreach (var user in users)
        _users.Add(user);
    }
  }
}