
using System;
using System.Threading.Tasks;

namespace Jinroo;

public class CupidRole : ARole
{
  public CupidRole()
  {
    Team = RoleTeam.VILLAGE;
    Type = RoleType.VILLAGER;
  }
  public const string Key = "cupid";

  public override string GetKey() => Key;

  public override int GetOrder() => (int)RoleOrder.CUPID;

  public override string GetName() => "Cupid";

  public override string GetIcon() => "/textures/roles/card_role_cupid.svg";

  public List<Player> Lovers = new();

  public override string GetAbilityText()
  {
    return "During the first night, Cupid may link two players will become lovers. When a lover dies, the other  commits suicide.";
  }

  public override async Task OnNightTurn( TaskSource task )
  {

    if ( Player is null || !Player.IsAlive || Turn != 1 )
      return;

    Player.GiveVision();

    GameMode.GameState.Multicast_SetPhase( GamePhaseType.CUPID, GetTimeout() );
    GameMode.GameState.Multicast_SendServerMessage( "Cupid is deciding which players will be lovers." );

    var responseData = await RequestTask( task, new(), RealTime.Now + GetTimeout() );

    // We don't continue if it's  a bot.
    if ( Player.Controller is null )
      return;

    try
    {
      if ( !responseData.ContainsKey( "lovers" ) )
        return;

      var loversIndex = responseData.GetValueOrDefault( "lovers" ) as List<int>;

      // If we don't have the 2 lovers, stop here
      if ( loversIndex is null && loversIndex.Count != 2 )
        return;

      // We check if the two lovers sent are no the same, we stop here unless the player loves himself so much.
      if ( loversIndex[0] == loversIndex[1] )
        return;

      foreach ( var loverIndex in loversIndex )
      {
        var player = GameMode.Players[loverIndex];
        Lovers.Add( player );
      }


      // We inform the lovers are in couple.
      foreach ( var lover in Lovers )
      {
        lover.Controller?.Client_SetLovers( loversIndex );

        lover.JoinChat( GameChannels.LOVERS );

        lover.Controller?.Client_SendServerMessage( "You are now falling in love. You can communicate here with your partner without the others player can seeing.", ServerMessageType.INFO, GameChannels.LOVERS );

        // In case they are intially not in the same team, move them in a separate team where they 
        // need to be the two only alive for win.
        if ( Lovers[0].Role.Team != Lovers[1].Role.Team )
        {
          lover.Role.Team = RoleTeam.LOVERS;
          lover.Controller?.Client_SendServerMessage( "As you were initially not on the same team, your objective changes. You and your lover must be the only survivors to win.", ServerMessageType.INFO, GameChannels.LOVERS );
        }
      }


      // We update the lovers also for cupid 
      Player.Controller.Client_SetLovers( loversIndex );
    }

    finally { }

    Player.RestrictVision();
  }

  public override async Task OnPlayerDead( TaskSource task, Player victim )
  {
    if ( Lovers.Count != 2 && !Lovers.Contains( victim ) )
      return;

    var otherLover = Lovers.TakeWhile( player => player != victim ).FirstOrDefault();

    otherLover?.TryKill( KillReason.LOVER_IS_DEAD );
  }

  public override async Task Client_OnTaskRequest( PlayerController controller, Dictionary<string, object> requestData, TaskSource task, Guid token )
  {
    var timeoutTime = RealTime.Now + GetTimeout();

    var lovers = await controller.WaitingTargets( 2, false, timeoutTime );

    try
    {
      var loversIndex = lovers.Select( playerData => playerData.Index ).ToList();
      Dictionary<String, object> responseData = new() {
        { "lovers", loversIndex}
      };

      controller.Server_OnTaskResponse( responseData, token );
    }
    finally { }

  }


}