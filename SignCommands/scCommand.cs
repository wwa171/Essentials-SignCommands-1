using System.Collections.Generic;
using TShockAPI;

namespace SignCommands
{
    public class SignCommand : Command
    {
        private int _cooldown;
        public SignCommand(int coolDown, List<string> permissions, CommandDelegate cmd, params string[] names)
            : base(permissions, cmd, names)
        {
            _cooldown = coolDown;
        }
    }
}
