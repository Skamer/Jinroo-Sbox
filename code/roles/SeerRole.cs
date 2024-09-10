using System;
using System.Threading.Tasks;

namespace Jinroo;

public class SeerRole : ARole
{
  public SeerRole()
  {
    Team = RoleTeam.VILLAGE;
    Type = RoleType.VILLAGER;
  }

  public const string Key = "seer";

  public override string GetKey() => Key;

  public override int GetOrder() => (int)RoleOrder.SEER;

  public override string GetName() => "Seer";

  public override string GetBroadcastTaskText() => "The seer is currently looking into her crystal ball.";

  public override string GetIcon() => "/textures/roles/card_role_seer.svg";


  public override string GetAbilityText()
  {
    return "Can use its crytal ball for checking the role of a player each turn.";
  }

  public override async Task OnNightTurn( TaskSource task )
  {

    if ( Player is null || !Player.IsAlive )
      return;

    GameMode.GameState.Multicast_SetPhase( GamePhaseType.SEER, GetTimeout() );
    GameMode.GameState.Multicast_SendServerMessage( GetBroadcastTaskText(), ServerMessageType.INFO );


    // If the player is a bot, we simulate it.
    if ( Player.Controller is null )
    {
      await Bot_OnNightTurn( task );
      return;
    }

    Player.GiveVision();

    var responseData = await RequestTask( task, new(), RealTime.Now + GetTimeout() );

    try
    {
      var targetIndex = (int)responseData["target"];
      var target = GameMode.Players[targetIndex];

      Player.Controller.Client_SendServerMessage( $"You have inspected {target?.State?.Name} is {target?.Role?.GetName()}" );
    }
    catch ( Exception e ) { }

    Player.RestrictVision();
  }

  public async Task Bot_OnNightTurn( TaskSource task )
  {
    var randomDecisionTime = Game.Random.Next( 3, GetTimeout() );
    await task.Delay( 1000 * randomDecisionTime );
  }

  public override async Task Client_OnTaskRequest( PlayerController controller, Dictionary<string, object> requestData, TaskSource task, Guid token )
  {
    var timeoutTime = RealTime.Now + GetTimeout();


    controller.SetChoicesDescription( "You can inspect a player for checking its role." );
    controller.SetChoicesTitleIcon( GetIcon() );
    controller.SetChoicesTitle( "Interaction" );
    controller.AddChoice( "inspect", "Inspect a player", "icon:check", "green" );
    controller.SetChoicesPanelVisible( true );

    var choice = await controller.WaitingChoices( timeoutTime );
    controller.SetChoicesPanelVisible( false );

    if ( choice != "inspect" )
      return;

    var targets = await controller.WaitingTargets( 1, false, timeoutTime );

    if ( targets.Count != 1 )
      return;

    Dictionary<string, object> responseData = new()
    {
      { "target", targets.First().Index}
    };


    controller.Server_OnTaskResponse( responseData, token );
  }

}