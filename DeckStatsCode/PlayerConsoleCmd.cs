using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;

namespace DeckStats.DeckStatsCode;

public class PlayerConsoleCmd : AbstractConsoleCmd
{
    public override CmdResult Process(Player? issuingPlayer, string[] args)
    {
        if (issuingPlayer == null)
        {
            return new CmdResult(false, "Issuing player not valid.");
        }
        NCapstoneContainer.Instance.Open(NMultiplayerPlayerExpandedState.Create(issuingPlayer));
        return new CmdResult(true, "Opened Multiplayer Player Expanded State.");
    }

    public override string CmdName => "player";
    public override string Args => "";
    public override string Description => "Opens the player information window.";
    public override bool IsNetworked => false;
}